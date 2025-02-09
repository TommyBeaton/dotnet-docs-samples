// Copyright 2024 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License").
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using GeminiTesting.Interfaces;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using System.Text.Json;
using Value = Google.Protobuf.WellKnownTypes.Value;

namespace GeminiTesting;

public class OCRDocument : IModelFunction
{
    private string TestDocUrl =
        "https://witnessocrspikedev.blob.core.windows.net/test-files/test-register.pdf?sp=r&st=2025-02-08T17:48:13Z&se=2025-02-09T01:48:13Z&spr=https&sv=2022-11-02&sr=b&sig=r%2B9u%2F8CC177EsUsYzTzyRiH4jUEsJBZ6sAxuEok%2FIKU%3D";

    private string _googleProjectId = "witness-ai-trials";
    private string _googleLocation = "europe-west9"; //Paris
    private string _googleModel = "gemini-2.0-flash-001";
    private string _outputDirectory = @"C:\Users\tbeat\source\GitHub\google-dotnet-samples\outputs";


    public async Task Execute()
    {
        var res = await ProcessDocumentAsync("Return a copy of the following documents but as an OCR'd document so that I can select the text from it.", "ocr_document.pdf");
        Console.WriteLine($"OCR result saved to: {res}");
    }

    private async Task<string> ProcessDocumentAsync()
    {
        var predictionServiceClient = new PredictionServiceClientBuilder
        {
            Endpoint = $"{_googleLocation}-aiplatform.googleapis.com"
        }.Build();

        string prompt = "Return a copy of the following documents but as an OCR'd document so that I can select the text from it.";

        var generateContentRequest = new GenerateContentRequest
        {
            Model = $"projects/{_googleProjectId}/locations/{_googleLocation}/publishers/google/models/{_googleModel}",
            Contents =
            {
                new Content
                {
                    Role = "USER",
                    Parts =
                    {
                        new Part { Text = prompt },
                        new Part { FileData = new() { MimeType = "application/pdf", FileUri = TestDocUrl }}
                    }
                }
            }
        };

        GenerateContentResponse response = await predictionServiceClient.GenerateContentAsync(generateContentRequest);
        Console.WriteLine(JsonSerializer.Serialize(response));
        string responseText = response.Candidates[0].Content.Parts[0].Text;
        Console.WriteLine(responseText);

        return responseText;

    }

    //https://cloud.google.com/vertex-ai/generative-ai/docs/start/quickstarts/quickstart-multimodal#send-request-image

    private async Task<string> ProcessDocumentAsync(string prompt, string outputFileName)
    {
        var predictRequest = new PredictRequest
        {
            Endpoint = $"{_googleLocation}-aiplatform.googleapis.com",
            Instances =
            {
                GetInstances(prompt)
            },
            EndpointAsEndpointName = EndpointName.FromProjectLocationPublisherModel(_googleProjectId, _googleLocation, "google", _googleModel),
        };

        // Create the PredictionServiceClient.
        var client = await new PredictionServiceClientBuilder().BuildAsync();

        // Call the prediction API.
        PredictResponse response = await client.PredictAsync(predictRequest);

        // Extract the OCR result from the prediction.
        string ocrText = string.Empty;
        if (response.Predictions.Count > 0)
        {
            var prediction = response.Predictions[0];

            // Attempt to extract a "text" field (this depends on your model's response structure).
            if (prediction.KindCase == Value.KindOneofCase.StructValue &&
                prediction.StructValue.Fields.ContainsKey("text"))
            {
                ocrText = prediction.StructValue.Fields["text"].StringValue;
            }
            else if (prediction.KindCase == Value.KindOneofCase.StringValue)
            {
                ocrText = prediction.StringValue;
            }
            else
            {
                throw new Exception("Unexpected prediction format from Gemini OCR model.");
            }
        }
        else
        {
            throw new Exception("No predictions returned from Gemini OCR model.");
        }

        // Save the OCR result to the file system.
        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }
        string outputFilePath = Path.Combine(_outputDirectory, outputFileName);
        await File.WriteAllTextAsync(outputFilePath, ocrText);

        return outputFilePath;
    }

    private Value GetInstances(string prompt)
    {
        var contentStruct = new Struct();
        contentStruct.Fields.Add("role", Value.ForString("USER"));

        // Build the parts array.
        var partsList = new List<Value>();

        // First part: the text prompt.
        var textPart = new Struct();
        textPart.Fields.Add("text", Value.ForString(prompt));
        partsList.Add(Value.ForStruct(textPart));

        // Second part: the file data.
        var fileDataStruct = new Struct();
        fileDataStruct.Fields.Add("mime_type", Value.ForString("application/pdf"));
        fileDataStruct.Fields.Add("file_uri", Value.ForString(TestDocUrl));

        var filePart = new Struct();
        filePart.Fields.Add("file_data", Value.ForStruct(fileDataStruct));
        partsList.Add(Value.ForStruct(filePart));

        // Add the parts list to the content.
        var listValue = new ListValue();
        listValue.Values.AddRange(partsList);
        contentStruct.Fields.Add("parts", Value.ForList(partsList.ToArray()));

        // Wrap the content in a Value and add it to the instances list.
        return Value.ForStruct(contentStruct);
    }
}
