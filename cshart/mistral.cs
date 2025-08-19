using System.Text;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace MistralOCR
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string MISTRAL_API_BASE = "https://api.mistral.ai/v1";
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("MistralOCR Application");
            Console.WriteLine("Choose an option:");
            Console.WriteLine("1. Use Mistral OCR (original functionality)");
            Console.WriteLine("2. Test OpenAI Chat API (converted from Python)");
            Console.Write("Enter choice (1 or 2): ");
            
            string? choice = Console.ReadLine();
            
            if (choice == "2")
            {
                await UseOpenAIChat();
            }
            else
            {
                await Task.Run(() => UseMistroOCR()); // Fix the warning by using Task.Run for the sync method
            }
        }

        private static async void UseMistroOCR() {
            // Get API key from environment variable
            string? apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Error: MISTRAL_API_KEY environment variable is not set.");
                Console.WriteLine("Please set your Mistral API key: set MISTRAL_API_KEY=your_api_key_here");
                return;
            }

            // Set up HTTP client
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            // Path to the PDF file
            string pdfPath = Path.Combine("pdf", "sample.pdf");
            
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine($"Error: PDF file not found at {pdfPath}");
                return;
            }

            try
            {
                Console.WriteLine("Mistral OCR - Processing PDF file");
                Console.WriteLine($"Local file: {pdfPath}");
                Console.WriteLine();
                
                // Ask user if they want to use a saved URL or upload new file
                Console.WriteLine("Choose option:");
                Console.WriteLine("1. Upload new file (default)");
                Console.WriteLine("2. Use saved signed URL");
                Console.Write("Enter choice (1 or 2): ");
                
                string? choice = Console.ReadLine();
                string? signedUrl = null;
                
                if (choice == "2")
                {
                    Console.WriteLine();
                    Console.Write("Enter your saved signed URL: ");
                    signedUrl = Console.ReadLine();
                    
                    if (string.IsNullOrEmpty(signedUrl))
                    {
                        Console.WriteLine("No URL provided. Switching to file upload...");
                        choice = "1";
                    }
                    else
                    {
                        Console.WriteLine("Using provided signed URL...");
                    }
                }
                
                if (choice != "2" || string.IsNullOrEmpty(signedUrl))
                {
                    // Step 1: Upload the file to get a signed URL
                    Console.WriteLine();
                    Console.WriteLine("Step 1: Uploading file to Mistral...");
                    signedUrl = await UploadFileAndGetUrl(pdfPath);
                    
                    if (string.IsNullOrEmpty(signedUrl))
                    {
                        Console.WriteLine("Failed to upload file and get signed URL.");
                        return;
                    }
                    
                    Console.WriteLine("File uploaded successfully!");
                    Console.WriteLine();
                    Console.WriteLine("📋 SIGNED URL (save this for reuse):");
                    Console.WriteLine("═══════════════════════════════════════════════════════════════");
                    Console.WriteLine(signedUrl);
                    Console.WriteLine("═══════════════════════════════════════════════════════════════");
                    Console.WriteLine("💡 TIP: Save this URL - you can use it directly next time without uploading!");
                    Console.WriteLine();
                }

                // Step 2: Process with OCR endpoint (following documentation example)
                Console.WriteLine("Step 2: Processing with OCR endpoint...");
                await ProcessWithOCR(signedUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task<string?> UploadFileAndGetUrl(string filePath)
        {
            try
            {
                // Upload file
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent("ocr"), "purpose");
                
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                form.Add(fileContent, "file", Path.GetFileName(filePath));

                var uploadResponse = await httpClient.PostAsync($"{MISTRAL_API_BASE}/files", form);
                
                if (!uploadResponse.IsSuccessStatusCode)
                {
                    string errorContent = await uploadResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Upload failed ({uploadResponse.StatusCode}): {errorContent}");
                    return null;
                }

                string uploadResponseContent = await uploadResponse.Content.ReadAsStringAsync();
                dynamic? uploadResult = JsonConvert.DeserializeObject(uploadResponseContent);
                string? fileId = uploadResult?.id?.ToString();
                
                if (string.IsNullOrEmpty(fileId))
                {
                    Console.WriteLine("Failed to get file ID from upload response.");
                    return null;
                }

                // Get signed URL
                var urlResponse = await httpClient.GetAsync($"{MISTRAL_API_BASE}/files/{fileId}/url?expiry=24");
                
                if (!urlResponse.IsSuccessStatusCode)
                {
                    string errorContent = await urlResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Get URL failed ({urlResponse.StatusCode}): {errorContent}");
                    return null;
                }

                string urlResponseContent = await urlResponse.Content.ReadAsStringAsync();
                dynamic? urlResult = JsonConvert.DeserializeObject(urlResponseContent);
                return urlResult?.url?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Upload error: {ex.Message}");
                return null;
            }
        }

        private static async Task ProcessWithOCR(string documentUrl)
        {
            try
            {
                // This follows the exact structure from the documentation
                var requestPayload = new
                {
                    model = "mistral-ocr-latest",
                    document = new
                    {
                        type = "document_url",
                        document_url = documentUrl
                    },
                    include_image_base64 = true
                };

                string jsonPayload = JsonConvert.SerializeObject(requestPayload, Formatting.Indented);
                Console.WriteLine("Request payload:");
                Console.WriteLine(jsonPayload);
                Console.WriteLine();

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync($"{MISTRAL_API_BASE}/ocr", content);
                
                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    
                    // Save to file (like the -o flag in curl)
                    await File.WriteAllTextAsync("ocr_output.json", responseContent);
                    
                    Console.WriteLine("✅ OCR completed successfully!");
                    Console.WriteLine("📄 Results saved to: ocr_output.json");
                    Console.WriteLine();
                    
                    // Display summary
                    dynamic? result = JsonConvert.DeserializeObject(responseContent);
                    if (result?.text != null)
                    {
                        string extractedText = result.text.ToString();
                        Console.WriteLine($"📝 Extracted text length: {extractedText.Length} characters");
                        Console.WriteLine();
                        Console.WriteLine("📋 First 300 characters:");
                        Console.WriteLine("═══════════════════════════════════════");
                        Console.WriteLine(extractedText.Substring(0, Math.Min(300, extractedText.Length)));
                        if (extractedText.Length > 300)
                        {
                            Console.WriteLine("... (see full text in ocr_output.json)");
                        }
                        Console.WriteLine("═══════════════════════════════════════");
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ OCR API Error ({response.StatusCode}): {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Processing error: {ex.Message}");
            }
        }

        private static async Task UseOpenAIChat()
        {
            const string BASE_URL = "https://aips-ai-gateway.ue1.dev.ai-platform.int.wexfabric.com/";
            
            // Get API key from environment variable (you may need to set this)
            string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Warning: OPENAI_API_KEY environment variable is not set.");
                Console.WriteLine("Using empty key - this may work with some custom endpoints.");
                apiKey = ""; // Some custom endpoints don't require API keys
            }

            // Create HttpClientHandler to disable SSL verification (for local development only)
            var httpClientHandler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            // Create HttpClient with the handler
            var customHttpClient = new HttpClient(httpClientHandler);

            try
            {
                // Read the OCR output JSON file
                string ocrOutputPath = "ocr_output.json";
                string pdfPath = Path.Combine("pdf", "sample.pdf");
                
                if (!File.Exists(ocrOutputPath))
                {
                    Console.WriteLine($"Error: OCR output file not found at {ocrOutputPath}");
                    Console.WriteLine("Please run the OCR process first (option 1) to generate the OCR output.");
                    return;
                }

                string ocrJsonContent = await File.ReadAllTextAsync(ocrOutputPath);
                string jsonOutput = await File.ReadAllTextAsync("output_v1.json");
                
                // Configure OpenAI client with custom base URL
                var options = new OpenAIClientOptions()
                {
                    Endpoint = new Uri(BASE_URL)
                };
                
                var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
                var chatClient = openAIClient.GetChatClient("azure-gpt-4o");

                // Create the prompt with OCR content and file references
                string userPrompt = $@"Generate json object based on the pdf and the output file.

PDF File: {pdfPath}

OCR Content:
{ocrJsonContent}

Sample json output:
{jsonOutput}

Please analyze the OCR output above (which was extracted from the PDF) and generate a structured JSON object that summarizes or transforms the key information from the document.";

                // Create chat messages with file content included
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are a helpful assistant that analyzes documents and generates structured JSON objects based on their content."),
                    new UserChatMessage(userPrompt)
                };

                Console.WriteLine("Sending request to OpenAI API with OCR content...");
                Console.WriteLine($"Including content from: {ocrOutputPath}");
                Console.WriteLine($"PDF source: {pdfPath}");
                Console.WriteLine();
                
                // Send chat completion request
                var completion = await chatClient.CompleteChatAsync(messages);

                // Extract the assistant's response
                var assistantResponse = completion.Value.Content[0].Text;
                
                Console.WriteLine("OpenAI API Response:");
                Console.WriteLine("=" + new string('=', 50));
                Console.WriteLine(assistantResponse);
                Console.WriteLine("=" + new string('=', 50));

                // Optionally save the response to a file
                string outputPath = "openai_analysis_output.json";
                await File.WriteAllTextAsync(outputPath, assistantResponse);
                Console.WriteLine($"\n✅ Response saved to: {outputPath}");
                
                // Also display usage information
                if (completion.Value.Usage != null)
                {
                    Console.WriteLine($"\n📊 Token Usage:");
                    Console.WriteLine($"   Input tokens: {completion.Value.Usage.InputTokenCount}");
                    Console.WriteLine($"   Output tokens: {completion.Value.Usage.OutputTokenCount}");
                    Console.WriteLine($"   Total tokens: {completion.Value.Usage.TotalTokenCount}");
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Error: File not found - {ex.Message}");
                Console.WriteLine("Make sure the OCR output file exists before running this option.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling OpenAI API: {ex.Message}");
            }
            finally
            {
                // Clean up resources
                customHttpClient?.Dispose();
                httpClientHandler?.Dispose();
            }
        }
    }
}
