using System.Text.RegularExpressions;
using System.Web;

namespace SignalRChatServer.Infrastructure.Utils;
public static class HtmlSanitizer
{
    private static readonly string[] BlackListTags = { "script", "iframe", "object", "embed", "form" };
    private static readonly string[] BlackListAttributes = { "onload", "onclick", "onerror", "href", "src" };
    
    //Saniterar innehållet i strängen för att motverka XSS. 
    public static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        //Plocka bort taggar. 
        foreach (var tag in BlackListTags)
        {
            var tagRegex = new Regex($"<\\/?\\s*{tag}\\s*[^>]*>", RegexOptions.IgnoreCase);
            input = tagRegex.Replace(input, string.Empty);
        }

        // Plocka bort attribut. 
        foreach (var attr in BlackListAttributes)
        {
            var attrRegex = new Regex($"{attr}\\s*=\\s*['\"].*?['\"]", RegexOptions.IgnoreCase);
            input = attrRegex.Replace(input, string.Empty);
        }
        //Plocka bort javascriptlänkar. 
        var jsLinkRegex = new Regex(@"href\s*=\s*['""]javascript:[^'""]*['""]", RegexOptions.IgnoreCase);
        input = jsLinkRegex.Replace(input, string.Empty);
        //Plocka bort http & https länkar. 
        var plainLinkRegex = new Regex(@"(http|https):\/\/[^\s<>]+", RegexOptions.IgnoreCase);
        input = plainLinkRegex.Replace(input, string.Empty);

        return HttpUtility.HtmlEncode(input);
    }
}
