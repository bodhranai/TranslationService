using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Web;

public static async Task<IActionResult> Run(HttpRequestMessage req, ILogger log)
{
    log.LogInformation("C# HTTP trigger function processed a request.");
    // Get the message from the request body
    string message = await req.Content.ReadAsStringAsync();
    log.LogInformation($"Received message: {message}");
    // Get the target language code from the query string
    string targetLanguage = req.GetQueryNameValuePairs()
                                .FirstOrDefault(q => string.Compare(q.Key, "to", true) == 0)
                                .Value;
    if (string.IsNullOrWhiteSpace(targetLanguage))
    {
        // If no target language is specified, default to English
        targetLanguage = "en";
    }
    // Translate the message to English
    string textTranslationKey = Environment.GetEnvironmentVariable("TextTranslationKey");
    string textTranslationEndpoint = Environment.GetEnvironmentVariable("TextTranslationEndpoint");
    string uri = $"{textTranslationEndpoint}/translate?api-version=3.0&to={targetLanguage}";
    using (HttpClient client = new HttpClient())
    {
        using (HttpRequestMessage request = new HttpRequestMessage())
        {
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(uri);
            request.Content = new StringContent("[{'Text':'" + message + "'}]", Encoding.UTF8, "application/json");
            request.Headers.Add("Ocp-Apim-Subscription-Key", textTranslationKey);
            HttpResponseMessage response = await client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                string errorMessage = await response.Content.ReadAsStringAsync();
                log.LogError($"Failed to translate message: {errorMessage}");
                return new StatusCodeResult((int)response.StatusCode);
            }
            string responseContent = await response.Content.ReadAsStringAsync();
            JArray responseArray = JArray.Parse(responseContent);
            string translatedMessage = responseArray[0]["translations"]["text"].ToString();

            log.LogInformation($"Translated message: {translatedMessage}");
        }
        // Save the translation to the storage container
        string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        string containerName = Environment.GetEnvironmentVariable("StorageContainerName");
        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
        CloudBlobContainer container = blobClient.GetContainerReference(containerName);
        await container.CreateIfNotExistsAsync();
        string blobName = $"{Guid.NewGuid().ToString()}.txt";
        CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
        await blob.UploadTextAsync(translatedMessage);
        log.LogInformation($"Saved translation to {blob.Uri}");
        // Return the translated message in the response
        var responseMessage = new
        {
            message = translatedMessage
        };
        return new OkObjectResult(responseMessage);
    }
}