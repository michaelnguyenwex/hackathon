using System.Text;
using Newtonsoft.Json;

namespace MistralOCR
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string MISTRAL_API_BASE = "https://api.mistral.ai/v1";
        
        static async Task Main(string[] args)
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

                //Choose option 2 and use the existing URL: "https://mistralaifilesapiprodswe.blob.core.windows.net/fine-tune/2078188b-b9c1-4259-b731-b432002c75e1/39cc273e-9dc4-4c08-b0fc-43a8c8897dc5/49fbd67166694569ae6a144135d5887e.pdf?se=2025-08-20T15%3A23%3A03Z&sp=r&sv=2025-01-05&sr=b&sig=eMqTLTHe8HtXRphIPyF0XIi1iwuSVzeLwFhZmf7gpDM%3D
                
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
    }
}
