namespace Musicbox.Services;

public sealed class QuoteService
{
    private static readonly string[] Quotes =
    [
        "总有人间一两风，填我十万八千梦。",
        "把普通的日子过得浪漫一些。",
        "慢一点也没关系，方向对了就很好。",
        "你认真生活的样子，本身就很动人。",
        "愿你眼里有光，心里有海，脚下有路。",
        "今天也请继续喜欢自己一点点。",
        "把热爱装进行囊，日子就会闪光。",
        "所有微小的努力，都会在未来开花。",
        "别急，最好的节奏叫按自己的步伐前进。",
        "生活不是赶路，是感受路。"
    ];

    public string GetDailyQuote()
    {
        var index = DateTime.Now.DayOfYear % Quotes.Length;
        return Quotes[index];
    }
}
