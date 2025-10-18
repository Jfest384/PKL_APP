using System.Text.RegularExpressions;

public static class FileHelper
{
    public static string GetFileNameFromContentDisposition(HttpResponseMessage response)
    {
        if (response?.Content?.Headers == null)
            return "download.pdf";

        if (response.Content.Headers.TryGetValues("Content-Disposition", out var values))
        {
            var header = values.FirstOrDefault();
            if (header != null)
            {
                // 1️⃣ Prioritaskan filename biasa
                var match = Regex.Match(header, @"filename\=""?(?<file>[^\"";]+)""?");
                if (match.Success)
                    return match.Groups["file"].Value;

                // 2️⃣ Fallback ke filename* (UTF-8)
                var matchStar = Regex.Match(header, @"filename\*\=UTF\-8''(?<file>.+)");
                if (matchStar.Success)
                    return Uri.UnescapeDataString(matchStar.Groups["file"].Value);
            }
        }

        return "download.pdf";
    }
}