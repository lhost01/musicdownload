using System.Security.Cryptography;
using System.Text;
using Musicbox.Models;

namespace Musicbox.Services;

public sealed class AudioConverterService
{
    private readonly string[] _supportedFormats = ["mp3", "flac", "wav"];

    public IReadOnlyList<string> SupportedFormats => _supportedFormats;

    public bool IsValidNcmFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        if (!Path.GetExtension(filePath).Equals(".ncm", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < 1024)
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var header = new byte[8];
            stream.ReadExactly(header);
            return Encoding.ASCII.GetString(header) == "CTENFDAM";
        }
        catch
        {
            return false;
        }
    }

    public List<NcmFileInfo> ScanNcmFiles(string folderPath, SearchOption searchOption)
    {
        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        return Directory.GetFiles(folderPath, "*.ncm", searchOption)
            .Where(IsValidNcmFile)
            .Select(path =>
            {
                var fileInfo = new FileInfo(path);
                return new NcmFileInfo
                {
                    FileName = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    FileSize = fileInfo.Length
                };
            })
            .ToList();
    }

    public async Task<ConversionResult> ConvertAsync(
        NcmFileInfo fileInfo,
        string outputFormat,
        string outputFolder,
        IProgress<ConversionProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        if (!IsValidNcmFile(fileInfo.FullPath))
        {
            return new ConversionResult { Success = false, ErrorMessage = "无效的 NCM 文件" };
        }

        if (!_supportedFormats.Contains(outputFormat, StringComparer.OrdinalIgnoreCase))
        {
            return new ConversionResult { Success = false, ErrorMessage = $"不支持的输出格式：{outputFormat}" };
        }

        if (!Directory.Exists(outputFolder))
        {
            return new ConversionResult { Success = false, ErrorMessage = "输出文件夹不存在" };
        }

        try
        {
            fileInfo.Status = ConversionStatus.Converting;
            progressCallback?.Report(new ConversionProgress
            {
                FileName = fileInfo.FileName,
                Progress = 0,
                Status = ConversionStatus.Converting
            });

            var baseFileName = Path.GetFileNameWithoutExtension(fileInfo.FileName);
            var outputFileName = $"{baseFileName}.{outputFormat.ToLowerInvariant()}";
            var outputPath = Path.Combine(outputFolder, outputFileName);

            var counter = 1;
            while (File.Exists(outputPath))
            {
                outputFileName = $"{baseFileName}_{counter}.{outputFormat.ToLowerInvariant()}";
                outputPath = Path.Combine(outputFolder, outputFileName);
                counter++;
            }

            var success = await DecodeNcmFileAsync(fileInfo.FullPath, outputPath, progressCallback, cancellationToken);
            if (!success)
            {
                return new ConversionResult { Success = false, ErrorMessage = "解码失败" };
            }

            fileInfo.Status = ConversionStatus.Completed;
            fileInfo.Progress = 100;
            fileInfo.OutputPath = outputPath;

            progressCallback?.Report(new ConversionProgress
            {
                FileName = fileInfo.FileName,
                Progress = 100,
                Status = ConversionStatus.Completed
            });

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                OutputFilePath = outputPath,
                FileName = outputFileName
            };
        }
        catch (OperationCanceledException)
        {
            fileInfo.Status = ConversionStatus.Cancelled;
            return new ConversionResult { Success = false, ErrorMessage = "转换已取消", Cancelled = true };
        }
        catch (Exception ex)
        {
            fileInfo.Status = ConversionStatus.Failed;
            fileInfo.ErrorMessage = ex.Message;
            return new ConversionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<BatchConversionResult> ConvertBatchAsync(
        List<NcmFileInfo> files,
        string outputFormat,
        string outputFolder,
        IProgress<BatchConversionProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var result = new BatchConversionResult
        {
            TotalFiles = files.Count
        };

        if (files.Count == 0)
        {
            return result;
        }

        var fileIndex = 0;
        foreach (var file in files)
        {
            fileIndex++;

            var fileProgress = new Progress<ConversionProgress>(progress =>
            {
                progressCallback?.Report(new BatchConversionProgress
                {
                    CurrentFile = progress.FileName,
                    CurrentFileProgress = progress.Progress,
                    OverallProgress = (int)(((fileIndex - 1) + progress.Progress / 100d) / files.Count * 100),
                    CurrentStatus = progress.Status,
                    ErrorMessage = progress.ErrorMessage
                });
            });

            var conversionResult = await ConvertAsync(file, outputFormat, outputFolder, fileProgress, cancellationToken);

            if (conversionResult.Success)
            {
                result.CompletedFiles++;
                if (!string.IsNullOrWhiteSpace(conversionResult.OutputFilePath))
                {
                    result.ConvertedFilePaths.Add(conversionResult.OutputFilePath);
                }
            }
            else if (conversionResult.Cancelled)
            {
                result.CancelledFiles++;
            }
            else
            {
                result.FailedFiles++;
            }
        }

        return result;
    }

    private static async Task<bool> DecodeNcmFileAsync(
        string inputPath,
        string outputPath,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(stream);

                var header = reader.ReadBytes(8);
                if (Encoding.ASCII.GetString(header) != "CTENFDAM")
                {
                    return false;
                }

                reader.ReadBytes(2);

                var keyLength = reader.ReadInt32();
                if (keyLength <= 0 || keyLength > 10000)
                {
                    return false;
                }

                var encryptedKey = reader.ReadBytes(keyLength);
                var xoredKey = encryptedKey.Select(value => (byte)(value ^ 0x64)).ToArray();

                var possibleAesKeys = new byte[][]
                {
                    [0x68, 0x7A, 0x48, 0x52, 0x41, 0x6D, 0x73, 0x6F, 0x35, 0x6B, 0x49, 0x6E, 0x62, 0x61, 0x78, 0x57],
                    [0x6A, 0x75, 0x73, 0x74, 0x20, 0x61, 0x6E, 0x6F, 0x74, 0x68, 0x65, 0x72, 0x20, 0x6E, 0x65, 0x74],
                    [0x6E, 0x65, 0x74, 0x65, 0x61, 0x73, 0x65, 0x63, 0x6C, 0x6F, 0x75, 0x64, 0x6D, 0x75, 0x73, 0x69],
                    [0x32, 0x39, 0x39, 0x33, 0x39, 0x31, 0x36, 0x36, 0x33, 0x30, 0x31, 0x30, 0x30, 0x00, 0x00, 0x00]
                };

                byte[]? decryptedKey = null;
                foreach (var aesKey in possibleAesKeys)
                {
                    try
                    {
                        var test = AesEcbDecrypt(xoredKey, aesKey);
                        if (test.Length >= 17 && Encoding.ASCII.GetString(test, 0, 17) == "neteasecloudmusic")
                        {
                            decryptedKey = test;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }

                decryptedKey ??= AesEcbDecrypt(xoredKey, possibleAesKeys[0]);
                var rc4Key = ExtractRc4Key(decryptedKey);

                var metaLength = reader.ReadInt32();
                if (metaLength > 0)
                {
                    reader.ReadBytes(metaLength);
                }

                if (stream.Length - stream.Position < 13)
                {
                    return false;
                }

                reader.ReadBytes(4);
                reader.ReadBytes(5);
                var imageLength = reader.ReadInt32();
                if (imageLength > 0)
                {
                    reader.ReadBytes(imageLength);
                }

                var audioLength = stream.Length - stream.Position;
                var buffer = new byte[8192];
                long totalRead = 0;

                using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                var rc4 = new Rc4Cipher(rc4Key);
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rc4.Decrypt(buffer, 0, bytesRead);
                    output.Write(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    progress?.Report(new ConversionProgress
                    {
                        FileName = Path.GetFileName(inputPath),
                        Progress = audioLength == 0 ? 100 : (int)(totalRead * 100 / audioLength),
                        Status = ConversionStatus.Converting
                    });
                }

                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    private static byte[] ExtractRc4Key(byte[] decryptedKey)
    {
        if (decryptedKey.Length < 17 || Encoding.ASCII.GetString(decryptedKey, 0, 17) != "neteasecloudmusic")
        {
            return decryptedKey;
        }

        var raw = new byte[decryptedKey.Length - 17];
        Array.Copy(decryptedKey, 17, raw, 0, raw.Length);
        return StripPkcs7Suffix(raw);
    }

    private static byte[] StripPkcs7Suffix(byte[] data)
    {
        if (data.Length == 0)
        {
            return data;
        }

        var pad = data[^1];
        if (pad is < 1 or > 16 || pad > data.Length)
        {
            return data;
        }

        for (var index = data.Length - pad; index < data.Length; index++)
        {
            if (data[index] != pad)
            {
                return data;
            }
        }

        var trimmed = new byte[data.Length - pad];
        Array.Copy(data, trimmed, trimmed.Length);
        return trimmed;
    }

    private static byte[] AesEcbDecrypt(byte[] encrypted, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Key = key;
        aes.Padding = PaddingMode.None;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
    }
}

public sealed class Rc4Cipher
{
    private readonly byte[] _s = new byte[256];
    private int _i;
    private int _j;

    public Rc4Cipher(byte[] key)
    {
        if (key.Length == 0)
        {
            throw new ArgumentException("Key cannot be empty.", nameof(key));
        }

        for (var index = 0; index < 256; index++)
        {
            _s[index] = (byte)index;
        }

        var j = 0;
        for (var index = 0; index < 256; index++)
        {
            j = (j + _s[index] + key[index % key.Length]) & 0xFF;
            (_s[index], _s[j]) = (_s[j], _s[index]);
        }
    }

    public void Decrypt(byte[] data, int offset, int length)
    {
        for (var index = offset; index < offset + length; index++)
        {
            _i = (_i + 1) & 0xFF;
            _j = (_i + _s[_i]) & 0xFF;
            data[index] ^= _s[(_s[_i] + _s[_j]) & 0xFF];
        }
    }
}

public sealed class ConversionResult
{
    public bool Success { get; init; }

    public string OutputPath { get; init; } = string.Empty;

    public string OutputFilePath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public bool Cancelled { get; init; }
}

public sealed class BatchConversionResult
{
    public int TotalFiles { get; init; }

    public int CompletedFiles { get; set; }

    public int FailedFiles { get; set; }

    public int CancelledFiles { get; set; }

    public List<string> ConvertedFilePaths { get; } = [];
}

public sealed class ConversionProgress
{
    public string FileName { get; init; } = string.Empty;

    public int Progress { get; init; }

    public ConversionStatus Status { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class BatchConversionProgress
{
    public string CurrentFile { get; init; } = string.Empty;

    public int CurrentFileProgress { get; init; }

    public int OverallProgress { get; init; }

    public ConversionStatus CurrentStatus { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
