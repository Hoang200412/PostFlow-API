using System;
using System.Net;
using System.Threading.Tasks;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class ImageGenerationService : IImageGenerationService
{
    public Task<string> GenerateImageUrlAsync(
        string prompt,
        string style,
        string aspectRatio,
        string colorTone,
        string mood,
        bool includeText,
        string? referenceImageUrl = null)
    {
        var fullPrompt = $"{prompt}, {style} style, {colorTone} color palette, {mood} mood";
        if (includeText)
        {
            fullPrompt += ", clean readable Vietnamese typography, social media post design";
        }

        if (!string.IsNullOrWhiteSpace(referenceImageUrl))
        {
            fullPrompt += ", inspired by the provided reference image";
        }

        var encodedPrompt = WebUtility.UrlEncode(fullPrompt);
        var seed = Math.Abs(HashCode.Combine(prompt, style, aspectRatio, colorTone, mood));
        var url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width={GetWidth(aspectRatio)}&height={GetHeight(aspectRatio)}&seed={seed}&nologo=true";
        return Task.FromResult(url);
    }

    private static int GetWidth(string aspectRatio) => aspectRatio switch
    {
        "4:5" => 1024,
        "16:9" => 1280,
        "9:16" => 720,
        _ => 1024
    };

    private static int GetHeight(string aspectRatio) => aspectRatio switch
    {
        "4:5" => 1280,
        "16:9" => 720,
        "9:16" => 1280,
        _ => 1024
    };
}
