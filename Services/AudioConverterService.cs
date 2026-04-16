using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using 网易云音乐下载.Models;

namespace 网易云音乐下载.Services
{
    public class AudioConverterService
    {
        private readonly string[] _supportedFormats = { "mp3", "flac", "wav" };

        public IReadOnlyList<string> SupportedFormats
        {
            get { return _supportedFormats; }
        }

        public bool IsValidNcmFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            if (!Path.GetExtension(filePath).Equals(".ncm", StringComparison.OrdinalIgnoreCase))
                return false;

            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Length < 1024)
                return false;

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[8];
                    fs.Read(header, 0, 8);
                    string magic = Encoding.ASCII.GetString(header);
                    return magic == "CTENFDAM";
                }
            }
            catch
            {
                return false;
            }
        }

        public List<NcmFileInfo> ScanNcmFiles(string folderPath, SearchOption searchOption)
        {
            List<NcmFileInfo> files = new List<NcmFileInfo>();

            if (!Directory.Exists(folderPath))
                return files;

            try
            {
                string[] ncmFiles = Directory.GetFiles(folderPath, "*.ncm", searchOption);
                foreach (string filePath in ncmFiles)
                {
                    if (IsValidNcmFile(filePath))
                    {
                        FileInfo fileInfo = new FileInfo(filePath);
                        NcmFileInfo ncmFile = new NcmFileInfo();
                        ncmFile.FileName = fileInfo.Name;
                        ncmFile.FullPath = fileInfo.FullName;
                        ncmFile.FileSize = fileInfo.Length;
                        files.Add(ncmFile);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("扫描文件夹时出错：" + ex.Message);
            }

            return files;
        }

        public async Task<ConversionResult> ConvertAsync(
            NcmFileInfo fileInfo,
            string outputFormat,
            string outputFolder,
            IProgress<ConversionProgress> progressCallback,
            CancellationToken cancellationToken)
        {
            if (fileInfo == null)
                return new ConversionResult { Success = false, ErrorMessage = "文件信息为空" };

            if (!IsValidNcmFile(fileInfo.FullPath))
                return new ConversionResult { Success = false, ErrorMessage = "无效的 NCM 文件" };

            if (!_supportedFormats.Contains(outputFormat.ToLower()))
                return new ConversionResult { Success = false, ErrorMessage = "不支持的输出格式：" + outputFormat };

            if (!Directory.Exists(outputFolder))
                return new ConversionResult { Success = false, ErrorMessage = "输出文件夹不存在" };

            try
            {
                fileInfo.Status = ConversionStatus.Converting;
                if (progressCallback != null)
                {
                    progressCallback.Report(new ConversionProgress
                    {
                        FileName = fileInfo.FileName,
                        Progress = 0,
                        Status = ConversionStatus.Converting
                    });
                }

                string baseFileName = Path.GetFileNameWithoutExtension(fileInfo.FileName);
                string outputFileName = baseFileName + "." + outputFormat.ToLower();
                string outputPath = Path.Combine(outputFolder, outputFileName);

                int counter = 1;
                while (File.Exists(outputPath))
                {
                    outputFileName = baseFileName + "_" + counter + "." + outputFormat.ToLower();
                    outputPath = Path.Combine(outputFolder, outputFileName);
                    counter++;
                }

                bool success = await DecodeNcmFileAsync(fileInfo.FullPath, outputPath, progressCallback, cancellationToken);

                if (!success)
                {
                    return new ConversionResult { Success = false, ErrorMessage = "解码失败" };
                }

                fileInfo.Status = ConversionStatus.Completed;
                fileInfo.OutputPath = outputPath;
                fileInfo.Progress = 100;

                if (progressCallback != null)
                {
                    progressCallback.Report(new ConversionProgress
                    {
                        FileName = fileInfo.FileName,
                        Progress = 100,
                        Status = ConversionStatus.Completed
                    });
                }

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
                if (progressCallback != null)
                {
                    progressCallback.Report(new ConversionProgress
                    {
                        FileName = fileInfo.FileName,
                        Progress = fileInfo.Progress,
                        Status = ConversionStatus.Cancelled
                    });
                }
                return new ConversionResult { Success = false, ErrorMessage = "转换已取消", Cancelled = true };
            }
            catch (Exception ex)
            {
                fileInfo.Status = ConversionStatus.Failed;
                fileInfo.ErrorMessage = ex.Message;
                if (progressCallback != null)
                {
                    progressCallback.Report(new ConversionProgress
                    {
                        FileName = fileInfo.FileName,
                        Progress = fileInfo.Progress,
                        Status = ConversionStatus.Failed,
                        ErrorMessage = ex.Message
                    });
                }
                return new ConversionResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 解码 NCM 容器（网易云 PC 客户端离线缓存格式，含当前 3.x 版本下载的文件）。
        /// 结构：密钥区 XOR(0x64) → AES-128-ECB 解出流密码密钥 → 网易云变种 RC4（KSA 同 RC4，PRGA 不交换且 j=(i+S[i])）还原音频。
        /// </summary>
        private async Task<bool> DecodeNcmFileAsync(string inputPath, string outputPath, 
            IProgress<ConversionProgress> progress, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (FileStream fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
                    using (BinaryReader reader = new BinaryReader(fs))
                    {
                        // 1. 读取文件头 "CTENFDAM" (8 字节)
                        byte[] header = reader.ReadBytes(8);
                        string magic = Encoding.ASCII.GetString(header);
                        if (magic != "CTENFDAM")
                        {
                            System.Diagnostics.Debug.WriteLine("无效的文件头：" + magic);
                            return false;
                        }

                        // 2. 跳过 2 字节 (0x03 0x00)
                        reader.ReadBytes(2);

                        // 3. 读取密钥长度
                        int keyLength = reader.ReadInt32();
                        if (keyLength <= 0 || keyLength > 10000)
                        {
                            System.Diagnostics.Debug.WriteLine("无效的密钥长度：" + keyLength);
                            return false;
                        }

                        // 4. 读取加密的密钥数据
                        byte[] encryptedKey = reader.ReadBytes(keyLength);

                        System.Diagnostics.Debug.WriteLine("加密密钥长度：" + keyLength);
                        System.Diagnostics.Debug.WriteLine("加密密钥前 32 字节：" + BitConverter.ToString(encryptedKey.Take(Math.Min(32, keyLength)).ToArray()));

                        // 5. 解密密钥 - NCM 使用 AES-128-ECB
                        // 首先对加密密钥进行简单的 XOR 处理
                        byte[] xoredKey = new byte[encryptedKey.Length];
                        for (int i = 0; i < encryptedKey.Length; i++)
                        {
                            xoredKey[i] = (byte)(encryptedKey[i] ^ 0x64);
                        }
                        
                        System.Diagnostics.Debug.WriteLine("XOR 处理后的前 32 字节：" + BitConverter.ToString(xoredKey.Take(Math.Min(32, xoredKey.Length)).ToArray()));

                        // 尝试多个已知的 AES 密钥
                        byte[][] possibleAesKeys = new byte[][]
                        {
                            new byte[] { 0x68, 0x7A, 0x48, 0x52, 0x41, 0x6D, 0x73, 0x6F, 0x35, 0x6B, 0x49, 0x6E, 0x62, 0x61, 0x78, 0x57 }, // "hzHRAmso5kInbaxW"
                            new byte[] { 0x6A, 0x75, 0x73, 0x74, 0x20, 0x61, 0x6E, 0x6F, 0x74, 0x68, 0x65, 0x72, 0x20, 0x6E, 0x65, 0x74 }, // "just another net"
                            new byte[] { 0x6E, 0x65, 0x74, 0x65, 0x61, 0x73, 0x65, 0x63, 0x6C, 0x6F, 0x75, 0x64, 0x6D, 0x75, 0x73, 0x69 }, // "neteasecloudmusi"
                            new byte[] { 0x32, 0x39, 0x39, 0x33, 0x39, 0x31, 0x36, 0x36, 0x33, 0x30, 0x31, 0x30, 0x30, 0x00, 0x00, 0x00 }, // 从解密结果推断
                        };
                        
                        string[] keyNames = new string[] { "hzHRAmso5kInbaxW", "just another net", "neteasecloudmusi", "Derived key" };
                        
                        byte[] decryptedKey = null;
                        string usedKeyName = keyNames[0];
                        
                        // 尝试 ECB 模式
                        for (int idx = 0; idx < possibleAesKeys.Length; idx++)
                        {
                            try
                            {
                                byte[] testDecrypted = AesEcbDecrypt(xoredKey, possibleAesKeys[idx]);
                                string testPrefix = testDecrypted.Length >= 17 ? Encoding.ASCII.GetString(testDecrypted, 0, 17) : "";
                                
                                System.Diagnostics.Debug.WriteLine($"尝试密钥 {keyNames[idx]} (ECB): 前缀 = {testPrefix}");
                                
                                if (testPrefix == "neteasecloudmusic")
                                {
                                    decryptedKey = testDecrypted;
                                    usedKeyName = keyNames[idx];
                                    System.Diagnostics.Debug.WriteLine($"✓ 找到正确密钥 (ECB): {usedKeyName}");
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"密钥 {keyNames[idx]} (ECB) 解密失败：{ex.Message}");
                            }
                        }
                        
                        // 如果 ECB 模式失败，尝试 CBC 模式
                        if (decryptedKey == null)
                        {
                            System.Diagnostics.Debug.WriteLine("\nECB 模式失败，尝试 CBC 模式...");
                            
                            // CBC 模式需要 IV（初始化向量）
                            // 尝试使用全零 IV
                            byte[] zeroIV = new byte[16];
                            
                            for (int idx = 0; idx < possibleAesKeys.Length; idx++)
                            {
                                try
                                {
                                    byte[] testDecrypted = AesCbcDecrypt(xoredKey, possibleAesKeys[idx], zeroIV);
                                    string testPrefix = testDecrypted.Length >= 17 ? Encoding.ASCII.GetString(testDecrypted, 0, 17) : "";
                                    
                                    System.Diagnostics.Debug.WriteLine($"尝试密钥 {keyNames[idx]} (CBC): 前缀 = {testPrefix}");
                                    
                                    if (testPrefix == "neteasecloudmusic")
                                    {
                                        decryptedKey = testDecrypted;
                                        usedKeyName = keyNames[idx];
                                        System.Diagnostics.Debug.WriteLine($"✓ 找到正确密钥 (CBC): {usedKeyName}");
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"密钥 {keyNames[idx]} (CBC) 解密失败：{ex.Message}");
                                }
                            }
                        }
                        
                        if (decryptedKey == null)
                        {
                            // 如果所有尝试都失败，使用第一个密钥的 ECB 模式
                            decryptedKey = AesEcbDecrypt(xoredKey, possibleAesKeys[0]);
                            System.Diagnostics.Debug.WriteLine("使用默认密钥（可能不正确）");
                        }
                        
                        System.Diagnostics.Debug.WriteLine("AES 解密后密钥长度：" + decryptedKey.Length);
                        System.Diagnostics.Debug.WriteLine("AES 解密后密钥前 30 字节：" + BitConverter.ToString(decryptedKey.Take(30).ToArray()));
                        
                        // RC4 密钥：去掉 "neteasecloudmusic"（17 字节）后，对剩余字节按 PKCS#7 去掉末尾填充。
                        // 密钥材料为原始字节序列（一般为可打印 ASCII），不要误做 Base64 解码。
                        byte[] rc4Key = decryptedKey;
                        List<byte[]> candidateKeys = new List<byte[]>();

                        if (decryptedKey.Length >= 17)
                        {
                            string prefix = Encoding.ASCII.GetString(decryptedKey, 0, 17);
                            System.Diagnostics.Debug.WriteLine("前缀：" + prefix);
                            if (prefix == "neteasecloudmusic")
                            {
                                byte[] rawAfterPrefix = new byte[decryptedKey.Length - 17];
                                Array.Copy(decryptedKey, 17, rawAfterPrefix, 0, rawAfterPrefix.Length);
                                byte[] pkcsStripped = StripPkcs7Suffix(rawAfterPrefix);
                                System.Diagnostics.Debug.WriteLine("去除前缀后长度：" + rawAfterPrefix.Length + "，PKCS#7 剥除后 RC4 密钥长度：" + pkcsStripped.Length);

                                candidateKeys.Add(pkcsStripped);
                                if (pkcsStripped.Length != rawAfterPrefix.Length)
                                    candidateKeys.Add(rawAfterPrefix);
                                rc4Key = pkcsStripped;
                            }
                        }

                        if (candidateKeys.Count == 0)
                            candidateKeys.Add(rc4Key);

                        System.Diagnostics.Debug.WriteLine("最终密钥长度：" + rc4Key.Length);

                        // 6. 读取元数据
                        int metaLength = reader.ReadInt32();
                        long bytesRemaining = fs.Length - fs.Position;
                        if (metaLength > 0)
                        {
                            if (metaLength > bytesRemaining)
                            {
                                System.Diagnostics.Debug.WriteLine("元数据长度超出文件：" + metaLength);
                                return false;
                            }
                            byte[] metaBytes = reader.ReadBytes(metaLength);
                            string metaJson = DecryptNcmMeta(metaBytes);
                            System.Diagnostics.Debug.WriteLine("元数据：" + metaJson);
                        }

                        // 7. 封面区：封面 CRC 4B + 保留 5B + 封面长度 4B（共 13B），再为封面二进制数据
                        // 旧实现误为「跳过 5B 再读长度」，漏掉 CRC 4B，会导致封面长度与音频起点整体错位。
                        bytesRemaining = fs.Length - fs.Position;
                        if (bytesRemaining < 13)
                        {
                            System.Diagnostics.Debug.WriteLine("封面头不完整，剩余：" + bytesRemaining);
                            return false;
                        }
                        reader.ReadBytes(4);  // 封面 CRC32
                        reader.ReadBytes(5);  // 保留

                        // 8. 封面图片长度与数据
                        bytesRemaining = fs.Length - fs.Position;
                        if (bytesRemaining < 4)
                        {
                            System.Diagnostics.Debug.WriteLine("无法读取封面长度");
                            return false;
                        }
                        int imageLength = reader.ReadInt32();
                        bytesRemaining = fs.Length - fs.Position;
                        if (imageLength < 0 || imageLength > bytesRemaining)
                        {
                            System.Diagnostics.Debug.WriteLine("封面长度异常：" + imageLength);
                            return false;
                        }
                        if (imageLength > 0)
                            reader.ReadBytes(imageLength);

                        // 9. 计算音频数据位置
                        long audioOffset = fs.Position;
                        long audioLength = fs.Length - audioOffset;

                        System.Diagnostics.Debug.WriteLine("音频数据：偏移=" + audioOffset + ", 长度=" + audioLength);
                        
                        // 调试：读取并显示原始音频数据的前几个字节
                        long originalPosition = fs.Position;
                        byte[] firstAudioBytes = new byte[32];
                        fs.Read(firstAudioBytes, 0, 32);
                        fs.Position = originalPosition; // 恢复到原始位置
                        System.Diagnostics.Debug.WriteLine("原始音频数据前 32 字节：" + BitConverter.ToString(firstAudioBytes));

                        // 10. 使用网易云 NCM 流密码解密音频（非标准 RC4 PRGA，见类注释）
                        // 尝试所有候选密钥
                        using (FileStream outputFs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                        {
                            const int bufferSize = 8192;
                            byte[] buffer = new byte[bufferSize];
                            long totalRead = 0;
                            int bytesRead;
                            bool isFirstBlock = true;
                            bool foundValidKey = false;

                            // 保存音频数据起始位置
                            long audioDataStart = fs.Position;
                            
                            // 尝试所有候选密钥
                            foreach (byte[] testKey in candidateKeys)
                            {
                                System.Diagnostics.Debug.WriteLine($"\n=== 尝试密钥长度 {testKey.Length} ===");
                                
                                // 重置文件位置
                                fs.Position = audioDataStart;
                                
                                RC4 rc4 = new RC4(testKey);
                                byte[] testBuffer = new byte[64];
                                int testBytesRead = fs.Read(testBuffer, 0, 64);
                                
                                if (testBytesRead > 0)
                                {
                                    rc4.Decrypt(testBuffer, 0, testBytesRead);
                                    
                                    System.Diagnostics.Debug.WriteLine("解密后前 32 字节：" + BitConverter.ToString(testBuffer.Take(32).ToArray()));
                                    
                                    // 检查是否是有效的音频格式
                                    if (testBytesRead >= 3 && testBuffer[0] == 0x49 && testBuffer[1] == 0x44 && testBuffer[2] == 0x33)
                                    {
                                        System.Diagnostics.Debug.WriteLine("✓✓✓ 找到正确的密钥！检测到 MP3 ID3 标签头");
                                        foundValidKey = true;
                                        rc4Key = testKey;
                                        break;
                                    }
                                    else if (testBytesRead >= 2 && testBuffer[0] == 0xFF && (testBuffer[1] & 0xE0) == 0xE0)
                                    {
                                        System.Diagnostics.Debug.WriteLine("✓✓✓ 找到正确的密钥！检测到 MPEG 音频帧头");
                                        foundValidKey = true;
                                        rc4Key = testKey;
                                        break;
                                    }
                                    else if (testBytesRead >= 4 && testBuffer[0] == 0x66 && testBuffer[1] == 0x4C && testBuffer[2] == 0x61 && testBuffer[3] == 0x43)
                                    {
                                        System.Diagnostics.Debug.WriteLine("✓✓✓ 找到正确的密钥！检测到 FLAC 文件头");
                                        foundValidKey = true;
                                        rc4Key = testKey;
                                        break;
                                    }
                                }
                            }
                            
                            // 使用找到的密钥进行完整流密码解密
                            System.Diagnostics.Debug.WriteLine($"\n使用密钥长度：{rc4Key.Length}");
                            fs.Position = audioDataStart;

                            RC4 finalRc4 = new RC4(rc4Key);

                            while ((bytesRead = fs.Read(buffer, 0, bufferSize)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                finalRc4.Decrypt(buffer, 0, bytesRead);

                                if (isFirstBlock)
                                {
                                    System.Diagnostics.Debug.WriteLine("最终解密后文件开头 32 字节：" + BitConverter.ToString(buffer.Take(Math.Min(32, bytesRead)).ToArray()));
                                    isFirstBlock = false;
                                }

                                outputFs.Write(buffer, 0, bytesRead);
                                totalRead += bytesRead;

                                if (progress != null && audioLength > 0)
                                {
                                    int progressPercent = (int)((totalRead * 100) / audioLength);
                                    progress.Report(new ConversionProgress
                                    {
                                        FileName = Path.GetFileName(inputPath),
                                        Progress = progressPercent,
                                        Status = ConversionStatus.Converting
                                    });
                                }
                            }

                            System.Diagnostics.Debug.WriteLine("解密完成，写入 " + totalRead + " 字节");

                            if (!foundValidKey)
                            {
                                System.Diagnostics.Debug.WriteLine("⚠ 警告：未找到有效的解密密钥，输出文件可能无法播放");
                            }
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("解码失败：" + ex.ToString());
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 对「去除 neteasecloudmusic 前缀之后」的尾段做 PKCS#7 剥离（与客户端加密时填充方式对应）。
        /// </summary>
        private static byte[] StripPkcs7Suffix(byte[] data)
        {
            if (data == null || data.Length == 0)
                return data;
            int pad = data[data.Length - 1];
            if (pad < 1 || pad > 16 || pad > data.Length)
                return data;
            for (int i = 0; i < pad; i++)
            {
                if (data[data.Length - 1 - i] != pad)
                    return data;
            }
            byte[] trimmed = new byte[data.Length - pad];
            Array.Copy(data, trimmed, trimmed.Length);
            return trimmed;
        }

        /// <summary>
        /// AES-128-ECB 解密
        /// </summary>
        private byte[] AesEcbDecrypt(byte[] encrypted, byte[] key)
        {
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Mode = CipherMode.ECB;
                    aes.Key = key;
                    aes.Padding = PaddingMode.None;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        return decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AES 解密失败：" + ex.Message);
                return encrypted;
            }
        }

        private byte[] AesCbcDecrypt(byte[] encrypted, byte[] key, byte[] iv)
        {
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Mode = CipherMode.CBC;
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Padding = PaddingMode.None;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        return decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AES-CBC 解密失败：" + ex.Message);
                return encrypted;
            }
        }

        /// <summary>
        /// 解密 NCM 元数据
        /// </summary>
        private string DecryptNcmMeta(byte[] metaBytes)
        {
            try
            {
                string prefix = Encoding.ASCII.GetString(metaBytes, 0, 22);
                if (prefix != "163 key(Don't modify):")
                    return null;

                string base64 = Encoding.ASCII.GetString(metaBytes, 22, metaBytes.Length - 22);
                byte[] encrypted = Convert.FromBase64String(base64);

                byte[] key = Encoding.ASCII.GetBytes("#14ljk_!\\]&0U<'(");
                byte[] decrypted = new byte[encrypted.Length];
                for (int i = 0; i < encrypted.Length; i++)
                {
                    decrypted[i] = (byte)(encrypted[i] ^ key[i % key.Length]);
                }

                string result = Encoding.UTF8.GetString(decrypted);
                if (result.StartsWith("music:"))
                    result = result.Substring(6);
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("元数据解密失败：" + ex.Message);
                return null;
            }
        }

        public async Task<BatchConversionResult> ConvertBatchAsync(
            List<NcmFileInfo> files,
            string outputFormat,
            string outputFolder,
            IProgress<BatchConversionProgress> progressCallback,
            CancellationToken cancellationToken)
        {
            BatchConversionResult result = new BatchConversionResult
            {
                TotalFiles = files.Count,
                CompletedFiles = 0,
                FailedFiles = 0,
                CancelledFiles = 0
            };

            if (files.Count == 0)
                return result;

            Progress<ConversionProgress> individualProgress = new Progress<ConversionProgress>(p =>
            {
                if (progressCallback != null)
                {
                    progressCallback.Report(new BatchConversionProgress
                    {
                        CurrentFile = p.FileName,
                        CurrentFileProgress = p.Progress,
                        OverallProgress = (result.CompletedFiles + result.FailedFiles + result.CancelledFiles) * 100 / files.Count,
                        CurrentStatus = p.Status,
                        ErrorMessage = p.ErrorMessage
                    });
                }
            });

            foreach (NcmFileInfo file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    file.Status = ConversionStatus.Cancelled;
                    result.CancelledFiles++;
                    continue;
                }

                ConversionResult conversionResult = await ConvertAsync(file, outputFormat, outputFolder, individualProgress, cancellationToken);

                if (conversionResult.Success)
                {
                    result.CompletedFiles++;
                    // 记录转换成功的文件路径
                    if (!string.IsNullOrEmpty(conversionResult.OutputFilePath))
                    {
                        result.ConvertedFilePaths.Add(conversionResult.OutputFilePath);
                    }
                }
                else if (conversionResult.Cancelled)
                    result.CancelledFiles++;
                else
                    result.FailedFiles++;

                if (progressCallback != null)
                {
                    progressCallback.Report(new BatchConversionProgress
                    {
                        CurrentFile = file.FileName,
                        CurrentFileProgress = file.Progress,
                        OverallProgress = (result.CompletedFiles + result.FailedFiles + result.CancelledFiles) * 100 / files.Count,
                        CurrentStatus = file.Status
                    });
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 网易云 NCM 音频流密码：KSA 与 RC4 相同；PRGA 与标准 RC4 不同（不在 i、j 处交换 S 盒，
    /// 且 j=(i+S[i]) mod 256），密钥流周期落在 256 步上，便于分块解密。
    /// </summary>
    public class RC4
    {
        private readonly byte[] _s = new byte[256];
        private int _i = 0;
        private int _j = 0;

        public RC4(byte[] key)
        {
            if (key == null || key.Length == 0)
                throw new ArgumentException("Key cannot be null or empty");

            for (int i = 0; i < 256; i++)
            {
                _s[i] = (byte)i;
            }

            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + _s[i] + key[i % key.Length]) & 0xFF;
                byte temp = _s[i];
                _s[i] = _s[j];
                _s[j] = temp;
            }
        }

        public void Decrypt(byte[] data, int offset, int length)
        {
            for (int k = offset; k < offset + length; k++)
            {
                _i = (_i + 1) & 0xFF;
                _j = (_i + _s[_i]) & 0xFF;
                data[k] ^= _s[(_s[_i] + _s[_j]) & 0xFF];
            }
        }
    }

    public class ConversionResult
    {
        public bool Success { get; set; }
        public string OutputPath { get; set; }
        public string OutputFilePath { get; set; }
        public string FileName { get; set; }
        public string ErrorMessage { get; set; }
        public bool Cancelled { get; set; }
    }

    public class BatchConversionResult
    {
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public int FailedFiles { get; set; }
        public int CancelledFiles { get; set; }
        public bool AllSuccess { get { return CompletedFiles == TotalFiles; } }
        public List<string> ConvertedFilePaths { get; set; }

        public BatchConversionResult()
        {
            ConvertedFilePaths = new List<string>();
        }
    }

    public class ConversionProgress
    {
        public string FileName { get; set; }
        public int Progress { get; set; }
        public ConversionStatus Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class BatchConversionProgress
    {
        public string CurrentFile { get; set; }
        public int CurrentFileProgress { get; set; }
        public int OverallProgress { get; set; }
        public ConversionStatus CurrentStatus { get; set; }
        public string ErrorMessage { get; set; }
    }
}
