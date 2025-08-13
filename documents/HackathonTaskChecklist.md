# Hackathon Task Checklist for OCR List + Click-to-Fill Application

1. Create Mock JSON Dataset for OCR Output
   - [ ] Write a JSON file (`mock_ocr_output.json`) with 2-3 pages, 15-20 snippets (lines and key-value pairs), and realistic client data (e.g., names, addresses, EINs).
   - [ ] Ensure the JSON matches the `AnalyzeResult` structure: `Pages` with `Lines` (Content, Polygon, Confidence), `KeyValuePairs` (Key/Value with Content, Polygon, Confidence), and minimal `Paragraphs`.
   - [ ] Include bounding polygons as [x1,y1,x2,y2,x3,y3,x4,y4] arrays (clockwise, pixel units) for overlay positioning.
   - [ ] Validate the JSON schema against the Azure SDK’s `AnalyzeResult` class using a JSON validator or manual deserialization test.
   - [ ] Store the file in the project’s `Resources` folder or a test directory.

2. Implement Mock Data Loader in WPF Application
   - [ ] Create a `MockDataProvider` class with a method `LoadMockAnalyzeResult` that deserializes `mock_ocr_output.json` into an `AnalyzeResult` object using System.Text.Json.
   - [ ] Add a boolean flag in app settings (e.g., `UseMockData`) to toggle between mock data and real OCR calls.
   - [ ] Modify the OCR integration code to check the flag and return the mock `AnalyzeResult` if enabled, bypassing Azure API calls.
   - [ ] Ensure the mock data is compatible with the `Snippet` class defined in Task 3 (e.g., Id, Text, BoundingPolygon, PageNumber).
   - [ ] Log when mock data is loaded and the number of snippets parsed for debugging.

3. Test Mock Data with Existing Parsing Logic
   - [ ] Update the snippet parsing logic to handle both real and mock `AnalyzeResult` inputs without code changes.
   - [ ] Run a unit test to deserialize the mock JSON and verify that 15-20 snippets are extracted with correct IDs, text, and polygons.
   - [ ] Check that key-value pairs are prioritized over lines for snippet creation, as per the original parsing logic.
   - [ ] Ensure bounding polygons are correctly mapped to the `Snippet` class for use in the PDF viewer.

4. Implement PDF Upload Functionality in the Application
   - [ ] Design a simple WPF user interface with a button labeled "Upload PDF" that triggers a file dialog for selecting a PDF file.
   - [ ] Use OpenFileDialog from System.Windows.Forms to allow the user to select a PDF file, ensuring the filter is set to ".pdf" files only.
   - [ ] Validate the selected file by checking its extension and size (limit to 50MB to avoid Azure limits).
   - [ ] Store the selected PDF file path in a class-level variable for later use in OCR processing.
   - [ ] Display a status message in the UI (e.g., via a TextBlock) confirming the file upload success or showing errors if the file is invalid.
   - [ ] Handle exceptions such as file not found or access denied during the upload process, logging them to a console or UI element.

5. Integrate Azure Document Intelligence SDK for OCR Extraction
   - [ ] Add the Azure.AI.DocumentIntelligence NuGet package (version 1.0.0-beta or latest compatible with v4.0) to the project.
   - [ ] Create a configuration class to hold Azure credentials: endpoint and key, loaded from app settings or environment variables.
   - [ ] Instantiate a DocumentIntelligenceClient using AzureKeyCredential and the endpoint URI.
   - [ ] Implement an asynchronous method to send the uploaded PDF as a stream to the AnalyzeDocumentAsync method using the "prebuilt-layout" model ID.
   - [ ] Include the optional features parameter set to "keyValuePairs" in the AnalyzeDocumentContent to enable extraction of key-value pairs, text, tables, and selection marks.
   - [ ] Use WaitUntil.Completed to await the operation and retrieve the AnalyzeResult object containing pages, lines, words, paragraphs, tables, and styles.
   - [ ] Handle API errors such as authentication failures or quota limits by catching RequestFailedException and displaying user-friendly messages.
   - [ ] Log the total number of pages processed and basic extraction stats (e.g., number of lines extracted) to the console for debugging.

6. Parse OCR Output to Identify and Assign Identifiers to Text Snippets
   - [ ] Create a data structure (e.g., List<Snippet> where Snippet has properties: Id (int), Text (string), BoundingPolygon (List<PointF>), PageNumber (int)) to store extracted snippets.
   - [ ] Iterate through the AnalyzeResult.Pages collection, and for each page, collect lines from page.Lines, words from page.Words, and key-value pairs if enabled.
   - [ ] Prioritize key-value pairs from result.KeyValuePairs (if using features=keyValuePairs) as primary snippets, falling back to individual lines or paragraphs for unstructured text.
   - [ ] Assign unique sequential identifiers (starting from 1) to each snippet, ensuring no duplicates across pages.
   - [ ] For each snippet, map the text content (e.g., from line.Content or keyValuePair.Value.Content) and its bounding polygon for overlay positioning.
   - [ ] Filter out irrelevant snippets like headers or footers by applying basic heuristics (e.g., skip if text is all uppercase and short, or based on position).
   - [ ] Store the parsed snippets in an in-memory collection (e.g., ObservableCollection for UI binding) sorted by page and position.
   - [ ] Implement a method to serialize this snippet list to JSON for temporary storage if the app needs to persist data between sessions.

7. Implement PDF Viewer Component with Identifier Overlays
   - [ ] Add a PDF viewer library such as PdfiumViewer or MoonPdfLib via NuGet to render PDF pages in the WPF window.
   - [ ] Create a custom WPF control (e.g., PdfViewerControl) that loads the uploaded PDF file and displays pages in a scrollable viewer.
   - [ ] For each page, calculate the rendering scale based on the viewer's DPI and the PDF's original dimensions from AnalyzeResult.Pages.
   - [ ] Overlay text blocks or labels on the PDF canvas for each snippet's identifier (e.g., a semi-transparent red number like "3" positioned at the centroid of the bounding polygon).
   - [ ] Use Canvas or Overlay elements in WPF to position the overlays absolutely, converting bounding polygon coordinates to screen coordinates.
   - [ ] Ensure overlays are non-interactive (e.g., IsHitTestVisible=false) to avoid interfering with PDF scrolling or zooming.
   - [ ] Add zoom functionality to the viewer, recalculating overlay positions dynamically on zoom changes.
   - [ ] Handle multi-page PDFs by lazy-loading pages and only rendering overlays for the currently visible page to optimize performance.
   - [ ] Style the overlays with configurable properties like font size, color, and opacity via application resources.

8. Create In-Memory Storage for Extracted Text Snippets
   - [ ] Define a Snippet class with properties: Id (int), Text (string), Confidence (float from OCR), EditableText (string for user modifications).
   - [ ] Use a Dictionary<int, Snippet> for fast lookup by identifier, populated after parsing the OCR output.
   - [ ] Implement thread-safe access to the dictionary using ConcurrentDictionary to handle potential background updates.
   - [ ] Add a method to update a snippet's text if the user edits it, ensuring changes are reflected in both storage and UI overlays.
   - [ ] Include a reset function to clear the storage when a new PDF is uploaded, disposing of any previous data.
   - [ ] Log storage stats (e.g., number of snippets stored) after population for debugging purposes.

9. Register Global Keyboard Hotkeys for Snippet Selection
   - [ ] Use Windows API interop (via P/Invoke) to register global hotkeys like Ctrl+1 through Ctrl+0 using RegisterHotKey from user32.dll.
   - [ ] Create a HotkeyManager class that overrides WndProc in the main WPF window to listen for WM_HOTKEY messages.
   - [ ] Map each hotkey (e.g., Ctrl+3 as MOD_CONTROL + Keys.D3) to a snippet ID (1-10 initially, expandable).
   - [ ] Handle hotkey registration failures by falling back to application-level key bindings if global registration isn't possible.
   - [ ] Unregister all hotkeys on application shutdown using UnregisterHotKey to avoid system conflicts.
   - [ ] Limit hotkeys to 20 max to control scope, with a configuration option to remap if needed.

10. Handle Hotkey Triggers to Copy and Paste Snippet Text
    - [ ] On hotkey detection, retrieve the corresponding Snippet from the dictionary using the ID.
    - [ ] Copy the snippet's Text (or EditableText if modified) to the system clipboard using Clipboard.SetText.
    - [ ] Simulate a paste action by sending Ctrl+V keystrokes to the foreground window using SendKeys.Send or keyboard simulation via InputSimulator library.
    - [ ] Add a delay (e.g., 100ms) between copy and paste to ensure clipboard reliability.
    - [ ] Handle cases where no snippet exists for the ID by showing a non-intrusive notification (e.g., system tray balloon tip).
    - [ ] Log each hotkey trigger event with the ID and text pasted for auditing.
    - [ ] Ensure the paste targets the currently focused field in another application (e.g., the web portal) by not bringing the app to foreground.

11. Add User Interface for Reviewing and Editing Extracted Snippets
    - [ ] Create a sidebar ListView in the WPF UI bound to the snippet collection, displaying ID, Text, and Confidence.
    - [ ] Make the list editable by using DataGrid with TextBox columns for modifying snippet Text.
    - [ ] Implement a "Validate All" button that highlights low-confidence snippets (e.g., <0.8) in red for user review.
    - [ ] Add tooltips on list items showing the original bounding polygon and page number for context.
    - [ ] Sync edits back to the storage dictionary and update PDF overlays if the text changes.
    - [ ] Provide a search/filter box to find snippets by partial text match for large PDFs.
    - [ ] Display a summary panel showing total snippets, average confidence, and any extraction warnings.

12. Implement Feedback Notifications for OCR and Hotkey Actions
    - [ ] Use ToastNotification or a custom WPF popup for success messages like "PDF Analyzed: 45 snippets extracted."
    - [ ] Show error toasts for OCR failures, e.g., "Azure API error: Invalid PDF format" with retry option.
    - [ ] On hotkey paste, display a brief toast "Pasted snippet #3: 'Sample Text'" to confirm action.
    - [ ] Integrate logging to a file or console for all notifications, timestamped for hackathon debugging.
    - [ ] Make notifications configurable (e.g., disable for production) via app settings.

13. Add Basic Error Handling and Edge Case Management
    - [ ] Handle OCR results with no snippets by displaying a message and disabling hotkeys.
    - [ ] Manage multi-page PDFs exceeding Azure limits (e.g., process only first 13 pages as per context).
    - [ ] Implement fallback for poor OCR confidence: allow manual snippet addition via UI.
    - [ ] Catch and handle exceptions in hotkey handling to prevent app crashes.
    - [ ] Add a timeout for Azure API calls (e.g., 60 seconds) with cancellation token.

14. Optimize Performance for 3-Day Hackathon Scope
    - [ ] Limit snippet count to 50 per PDF to avoid overload, with a warning if exceeded.
    - [ ] Use async/await throughout for non-blocking UI during OCR and rendering.
    - [ ] Cache OCR results in memory only, no persistent storage to keep simple.
    - [ ] Profile and optimize overlay rendering for large PDFs by batching updates.

15. Implement End-to-End Testing Functionality
    - [ ] Create a test method that simulates upload of a sample PDF URL (e.g., from Azure samples).
    - [ ] Verify extraction by asserting expected snippet count and sample texts.
    - [ ] Test hotkey by simulating key presses and checking clipboard content.
    - [ ] Add UI button to run tests and display pass/fail results.
    - [ ] Include manual test scenarios for user validation, like editing a snippet and pasting.