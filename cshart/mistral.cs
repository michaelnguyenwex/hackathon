using System.Text;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace MistralOCR
{
    class Program
    {
        private static readonly HttpClient httpClient = CreateHttpClientWithCorporateCert();
        private const string MISTRAL_API_BASE = "https://api.mistral.ai/v1";
        
        private static HttpClient CreateHttpClientWithCorporateCert()
        {
            try
            {
                // Load the corporate certificate
                string certPath = "corp-root.cer";
                if (!File.Exists(certPath))
                {
                    Console.WriteLine($"Warning: Certificate file '{certPath}' not found. Using default SSL validation.");
                    return new HttpClient() { Timeout = TimeSpan.FromMinutes(2) };
                }

                // Load the certificate
                X509Certificate2 corporateCert = new X509Certificate2(certPath);
                Console.WriteLine($"Loaded corporate certificate: {corporateCert.Subject}");

                // Create custom handler with certificate validation
                var httpClientHandler = new HttpClientHandler();
                httpClientHandler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // If there are no SSL policy errors, accept the certificate
                    if (sslPolicyErrors == SslPolicyErrors.None)
                        return true;

                    // If the only error is about the certificate chain, check against our corporate cert
                    if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors && chain?.ChainPolicy != null && certificate != null)
                    {
                        // Add our corporate certificate to the chain
                        chain.ChainPolicy.ExtraStore.Add(corporateCert);
                        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                        chain.ChainPolicy.CustomTrustStore.Add(corporateCert);
                        
                        // Re-build the chain with our corporate cert
                        bool isValid = chain.Build(new X509Certificate2(certificate));
                        
                        if (isValid)
                        {
                            Console.WriteLine("Certificate chain validated successfully with corporate certificate.");
                            return true;
                        }
                        
                        // Log chain status for debugging
                        Console.WriteLine($"Certificate chain validation failed. Chain status: {chain.ChainStatus.Length} errors");
                        foreach (var status in chain.ChainStatus)
                        {
                            Console.WriteLine($"  - {status.Status}: {status.StatusInformation}");
                        }
                    }

                    Console.WriteLine($"SSL Policy Error: {sslPolicyErrors}");
                    return false; // Reject certificate if validation fails
                };

                return new HttpClient(httpClientHandler) { Timeout = TimeSpan.FromMinutes(2) };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error configuring HttpClient with corporate certificate: {ex.Message}");
                Console.WriteLine("Falling back to default HttpClient configuration.");
                return new HttpClient() { Timeout = TimeSpan.FromMinutes(2) };
            }
        }
        
        static async Task Main(string[] args)
        {
            // Main function is now blank - call individual functions as needed
            // Example usage:
            await RunMistralOCRWithBase64("pdf/sample2.pdf");
            // await RunMistralOCRWithUpload("pdf/sample2.pdf");
            // await RunMistralOCRWithSavedUrl("pdf/sample2.pdf", "your-saved-url");
            // await RunOpenAIChat();
        }

        /// <summary>
        /// Process PDF using Mistral OCR with base64 encoding approach (recommended)
        /// </summary>
        /// <param name="pdfPath">Path to the PDF file to process</param>
        public static async Task RunMistralOCRWithBase64(string pdfPath)
        {
            // Get API key from environment variable
            string? apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Error: MISTRAL_API_KEY environment variable is not set.");
                Console.WriteLine("Please set your Mistral API key: set MISTRAL_API_KEY=your_api_key_here");
                return;
            }

            // Set up HTTP client
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            if (!File.Exists(pdfPath))
            {
                Console.WriteLine($"Error: PDF file not found at {pdfPath}");
                return;
            }

            try
            {
                Console.WriteLine("Mistral OCR - Processing PDF with Base64 approach");
                Console.WriteLine($"Local file: {pdfPath}");
                Console.WriteLine();
                
                Console.WriteLine("Step 1: Converting PDF to base64...");
                try
                {
                    string base64Pdf = await ConvertPdfToBase64(pdfPath);
                    string documentReference = $"data:application/pdf;base64,{base64Pdf}";
                    Console.WriteLine("✅ PDF converted to base64 successfully!");
                    Console.WriteLine($"📄 Base64 size: {base64Pdf.Length / 1024} KB");
                    Console.WriteLine($"📄 Full document_url: data:application/pdf;base64,[{base64Pdf.Length} chars]");
                    
                    Console.WriteLine();
                    Console.WriteLine("Step 2: Processing with OCR endpoint...");
                    await ProcessWithOCR(documentReference);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to convert PDF to base64: {ex.Message}");
                    Console.WriteLine("This could be due to file size or memory constraints.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Process PDF using Mistral OCR with file upload approach
        /// </summary>
        /// <param name="pdfPath">Path to the PDF file to process</param>
        public static async Task RunMistralOCRWithUpload(string pdfPath)
        {
            // Get API key from environment variable
            string? apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Error: MISTRAL_API_KEY environment variable is not set.");
                Console.WriteLine("Please set your Mistral API key: set MISTRAL_API_KEY=your_api_key_here");
                return;
            }

            // Set up HTTP client
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            if (!File.Exists(pdfPath))
            {
                Console.WriteLine($"Error: PDF file not found at {pdfPath}");
                return;
            }

            try
            {
                Console.WriteLine("Mistral OCR - Processing PDF with Upload approach");
                Console.WriteLine($"Local file: {pdfPath}");
                Console.WriteLine();
                
                Console.WriteLine("Step 1: Uploading file to Mistral...");
                string? documentReference = await UploadFileAndGetUrl(pdfPath);
                
                if (string.IsNullOrEmpty(documentReference))
                {
                    Console.WriteLine("Failed to upload file and get signed URL.");
                    Console.WriteLine("Falling back to base64 approach...");
                    string base64Pdf = await ConvertPdfToBase64(pdfPath);
                    documentReference = $"data:application/pdf;base64,{base64Pdf}";
                }
                else
                {
                    Console.WriteLine("File uploaded successfully!");
                    Console.WriteLine();
                    Console.WriteLine("📋 SIGNED URL (save this for reuse):");
                    Console.WriteLine("═══════════════════════════════════════════════════════════════");
                    Console.WriteLine(documentReference);
                    Console.WriteLine("═══════════════════════════════════════════════════════════════");
                    Console.WriteLine("💡 TIP: Save this URL - you can use it directly next time without uploading!");
                    Console.WriteLine();
                }

                Console.WriteLine("Step 2: Processing with OCR endpoint...");
                await ProcessWithOCR(documentReference);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Process PDF using Mistral OCR with a previously saved signed URL
        /// </summary>
        /// <param name="pdfPath">Path to the PDF file (used for fallback if URL fails)</param>
        /// <param name="savedUrl">Previously saved signed URL</param>
        public static async Task RunMistralOCRWithSavedUrl(string pdfPath, string savedUrl)
        {
            // Get API key from environment variable
            string? apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Error: MISTRAL_API_KEY environment variable is not set.");
                Console.WriteLine("Please set your Mistral API key: set MISTRAL_API_KEY=your_api_key_here");
                return;
            }

            // Set up HTTP client
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            try
            {
                Console.WriteLine("Mistral OCR - Processing PDF with Saved URL approach");
                Console.WriteLine($"Local file: {pdfPath}");
                Console.WriteLine($"Saved URL: {savedUrl}");
                Console.WriteLine();
                
                string documentReference = savedUrl;
                
                if (string.IsNullOrEmpty(documentReference))
                {
                    Console.WriteLine("No URL provided. Switching to base64 approach...");
                    if (!File.Exists(pdfPath))
                    {
                        Console.WriteLine($"Error: PDF file not found at {pdfPath}");
                        return;
                    }
                    Console.WriteLine("Converting PDF to base64...");
                    string base64Pdf = await ConvertPdfToBase64(pdfPath);
                    documentReference = $"data:application/pdf;base64,{base64Pdf}";
                }
                else
                {
                    Console.WriteLine("Using provided signed URL...");
                }

                Console.WriteLine("Step 1: Processing with OCR endpoint...");
                await ProcessWithOCR(documentReference);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task<string> ConvertPdfToBase64(string filePath)
        {
            try
            {
                Console.WriteLine($"Reading PDF file: {filePath}");
                
                // Check if file exists and get info
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"PDF file not found: {filePath}");
                }
                
                var fileInfo = new FileInfo(filePath);
                Console.WriteLine($"File size: {fileInfo.Length / 1024} KB");
                
                // Try synchronous read first
                Console.WriteLine("Reading file bytes...");
                byte[] pdfBytes = File.ReadAllBytes(filePath);
                Console.WriteLine($"Successfully read {pdfBytes.Length} bytes");
                
                Console.WriteLine("Converting to base64...");
                string base64String = Convert.ToBase64String(pdfBytes);
                Console.WriteLine($"Base64 conversion complete. Length: {base64String.Length}");
                
                return base64String;
            }
            catch (OutOfMemoryException ex)
            {
                Console.WriteLine($"Out of memory error during base64 conversion: {ex.Message}");
                Console.WriteLine("The PDF file might be too large. Try with a smaller file.");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting PDF to base64: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                throw;
            }
        }

        private static async Task<string?> UploadFileAndGetUrl(string filePath)
        {
            try
            {
                Console.WriteLine($"Preparing to upload: {filePath}");
                Console.WriteLine($"File size: {new FileInfo(filePath).Length / 1024} KB");
                Console.WriteLine($"Uploading to: {MISTRAL_API_BASE}/files");
                
                // Upload file
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent("ocr"), "purpose");
                
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                form.Add(fileContent, "file", Path.GetFileName(filePath));

                Console.WriteLine("Sending upload request...");
                var uploadResponse = await httpClient.PostAsync($"{MISTRAL_API_BASE}/files", form);
                Console.WriteLine($"Upload response status: {uploadResponse.StatusCode}");
                
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

        private static async Task ProcessWithOCR(string documentReference)
        {
            try
            {
                Console.WriteLine($"Processing document with Mistral OCR...");
                Console.WriteLine($"Document reference type: {(documentReference.StartsWith("data:") ? "Base64 Data URL" : "Signed URL")}");
                
                // This follows the exact structure from the documentation
                var requestPayload = new
                {
                    model = "mistral-ocr-latest",
                    document = new
                    {
                        type = "document_url",
                        document_url = documentReference
                    },
                    include_image_base64 = true
                };

                string jsonPayload = JsonConvert.SerializeObject(requestPayload, Formatting.Indented);
                
                // Show truncated payload for readability
                var debugPayload = JsonConvert.SerializeObject(new
                {
                    model = requestPayload.model,
                    document = new
                    {
                        type = requestPayload.document.type,
                        document_url = requestPayload.document.document_url.Length > 100 
                            ? requestPayload.document.document_url.Substring(0, 100) + "...[TRUNCATED]"
                            : requestPayload.document.document_url
                    },
                    include_image_base64 = requestPayload.include_image_base64
                }, Formatting.Indented);
                
                Console.WriteLine("Request payload (truncated for readability):");
                Console.WriteLine(debugPayload);
                Console.WriteLine($"📊 Full payload size: {jsonPayload.Length / 1024} KB");
                Console.WriteLine();

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                
                Console.WriteLine("Sending request to Mistral OCR API...");
                Console.WriteLine($"Request URL: {MISTRAL_API_BASE}/ocr");
                Console.WriteLine("Waiting for response...");
                
                HttpResponseMessage response;
                try
                {
                    response = await httpClient.PostAsync($"{MISTRAL_API_BASE}/ocr", content);
                    Console.WriteLine($"Response received! Status: {response.StatusCode}");
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    Console.WriteLine("❌ Request timed out after 2 minutes.");
                    Console.WriteLine("This might be due to network issues or the API being slow.");
                    return;
                }
                catch (TaskCanceledException ex)
                {
                    Console.WriteLine("❌ Request was canceled (likely timeout).");
                    Console.WriteLine($"Details: {ex.Message}");
                    return;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"❌ Network error: {ex.Message}");
                    Console.WriteLine("Check your internet connection and API endpoint.");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Unexpected error during HTTP request: {ex.Message}");
                    return;
                }
                
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

        /// <summary>
        /// Process OCR output using OpenAI Chat API for structured JSON generation
        /// </summary>
        public static async Task RunOpenAIChat()
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
                string pdfPath = Path.Combine("pdf", "sample2.pdf");
                
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
                var chatClient = openAIClient.GetChatClient("azure-gpt-4o-mini");

                // Create the prompt with OCR content and file references
                string userPrompt = $@"Generate json object based on the pdf and the output file.

PDF File: {pdfPath}

OCR Content:
{ocrJsonContent}

Sample json output template:
{jsonOutput}

Please analyze the OCR output above (which was extracted from the PDF) and generate a structured JSON object that summarizes or transforms the key information from the document. 
Rule: follow the sample json output template exactly.";

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
