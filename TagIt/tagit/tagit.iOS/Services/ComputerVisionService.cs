﻿using Foundation;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using tagit.Analysis;
using tagit.Common;
using tagit.iOS.Services;
using tagit.Services;
using UIKit;
using Xamarin.Forms;

[assembly: Dependency(typeof(ComputerVisionService))]
namespace tagit.iOS.Services
{
    ///Contains methods for accessing Microsoft Cognitive Services Computer Vision APIs
    public class ComputerVisionService : IComputerVisionService
    {
        public async Task<ImageAnalysisResult> AnalyzeImageAsync(byte[] bytes)
        {
            return await GetImageAnalysisAsync(bytes);
        }

        public async Task<ImageAnalysisResult> AnalyzeImageAsync(string url)
        {
            var bytes = await GetImageFromUriAsync(url);

            return await GetImageAnalysisAsync(bytes);
        }

        private async Task<byte[]> GetImageFromUriAsync(string uri)
        {
            using(var client = new HttpClient())
            {
                return await client.GetByteArrayAsync(new Uri(uri));
            }
        }

        private async Task<ImageAnalysisResult> GetImageAnalysisAsync(byte[] bytes)
        {
            ImageAnalysisResult result = null;

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", CoreConstants.ComputerVisionApiSubscriptionKey);

                    byte[] lowQualityImageBytes = null;

                    using (var data = NSData.FromArray(bytes))
                    {
                        var image = UIImage.LoadFromData(data);
                        var lowerQualityData = image.AsJPEG(0.1f);

                        lowQualityImageBytes = new byte[lowerQualityData.Length];
                        System.Runtime.InteropServices.Marshal.Copy(lowerQualityData.Bytes, lowQualityImageBytes, 0, Convert.ToInt32(lowerQualityData.Length));
                    }

                    var payload = new ByteArrayContent(lowQualityImageBytes);

                    payload.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    var analysisFeatures = "Color,ImageType,Tags,Categories,Description,Adult,Faces";

                    var uri = new Uri($"{CoreConstants.CognitiveServicesBaseUrl}/analyze?visualFeatures={analysisFeatures}");

                    using (var results = await client.PostAsync(uri, payload))
                    {
                        var analysisResults = await results.Content.ReadAsStringAsync();

                        var imageAnalysisResult = JsonConvert.DeserializeObject<ImageAnalysisInfo>(analysisResults);

                        result = new ImageAnalysisResult
                        {
                            id = Guid.NewGuid().ToString(),
                            details = imageAnalysisResult,
                            caption = imageAnalysisResult.description?.captions.FirstOrDefault()?.text,
                            tags = imageAnalysisResult.description?.tags.ToList()
                        };

                        if (string.IsNullOrEmpty(result.caption))
                        {
                            result.caption = "No caption";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ComputerVisionService.GetImageAnalysisAsync Exception: {ex}");
            }

            return result;
        }
    }
}