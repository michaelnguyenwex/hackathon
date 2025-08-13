# Product Requirements Document: OCR List + Click-to-Fill Application

## 1. Project Overview

### 1.1 Purpose
The "OCR List + Click-to-Fill" application aims to streamline the client onboarding process by reducing manual data entry from a 13-page PDF into a web portal. The application uses Azure Document Intelligence to extract text and key-value pairs from the PDF, displays them with numbered overlays, and allows users to populate form fields in the web portal using keyboard shortcuts (e.g., Ctrl+3). This MVP, developed for a 3-day hackathon, prioritizes simplicity, user validation, and integration with existing front-end validations, avoiding direct database writes to maintain data integrity.

### 1.2 Background
Currently, users manually copy and paste data from a 13-page client PDF into a multi-screen web portal, a process that is time-consuming and error-prone. An initial proposal to extract data and insert it directly into backend tables was deemed risky due to bypassing front-end validations (e.g., EIN or username checks). The proposed solution is a lightweight WPF application that runs alongside the web portal, extracts data via OCR, displays it with numbered identifiers, and allows users to paste data into focused fields using hotkeys, preserving existing validations.

### 1.3 Objectives
- Reduce manual data entry time by 50% for client onboarding.
- Leverage Azure Document Intelligence (v4.0, prebuilt-layout model) for accurate text and key-value pair extraction.
- Enable user validation of extracted data to ensure accuracy.
- Deliver a functional prototype within 3 days for hackathon demonstration.
- Use mock data to prevent OCR integration delays and unblock parallel development.

### 1.4 Scope
**In-Scope**:
- Upload and process a 13-page PDF using Azure Document Intelligence (prebuilt-layout model with `features=keyValuePairs`).
- Display PDF with numbered overlays (1-20) for extracted snippets (lines or key-value pairs).
- Allow users to select snippets via global hotkeys (Ctrl+1 to Ctrl+0) to paste into the web portal.
- Provide a sidebar for reviewing and editing extracted snippets.
- Support up to 50 snippets per PDF, with basic error handling and feedback notifications.
- Use mock data for development and testing to avoid OCR setup delays.

**Out-of-Scope**:
- Direct database integration or backend table writes.
- Advanced snippet filtering (e.g., complex heuristics for headers/footers).
- Persistent storage of OCR results or user edits.
- Support for non-PDF formats or PDFs exceeding 13 pages.
- Custom OCR model training or advanced Azure features.
- Mobile or web-based UI (WPF desktop app only).

## 2. Stakeholders
- **End Users**: Onboarding staff who manually enter client data into the web portal.
- **Development Team**: Three developers using C# and .NET, building the MVP in 3 days.
- **Hackathon Judges**: Evaluate the prototype for functionality, innovation, and usability.
- **Project Sponsor**: Team lead or manager overseeing the hackathon submission.

## 3. Functional Requirements

### 3.1 PDF Upload
- **Description**: Users can upload a PDF file (up to 13 pages, 50MB) via a WPF interface.
- **Requirements**:
  - Button to trigger OpenFileDialog, filtering for `.pdf` files.
  - Validate file extension and size, displaying errors for invalid files.
  - Store file path for OCR processing and PDF rendering.
- **Success Criteria**: User uploads a valid PDF, and the UI confirms success or shows clear error messages.

### 3.2 OCR Extraction with Azure Document Intelligence
- **Description**: Extract text, key-value pairs, and bounding polygons from the PDF using Azure Document Intelligence (v4.0, prebuilt-layout model).
- **Requirements**:
  - Use `Azure.AI.DocumentIntelligence` SDK (v1.0.0-beta or compatible) with `prebuilt-layout` model and `features=keyValuePairs`.
  - Process PDF as a stream, extracting lines, words, key-value pairs, and minimal paragraphs.
  - Support up to 13 pages, handling up to 50 snippets (lines or key-value values).
  - Log extraction stats (e.g., number of snippets) for debugging.
- **Success Criteria**: Extracts 15-20 snippets from a sample 2-3 page PDF with >90% accuracy (based on Azure’s confidence scores).

### 3.3 Mock Data for Development
- **Description**: Provide a mock JSON dataset to simulate `AnalyzeResult` output, enabling parallel UI and hotkey development.
- **Requirements**:
  - Create `mock_ocr_output.json` with 2-3 pages, 15-20 snippets (lines and key-value pairs), and realistic client data (e.g., names, EINs).
  - Match `AnalyzeResult` structure: `Pages` (Lines, Words), `KeyValuePairs`, minimal `Paragraphs`.
  - Implement a `MockDataProvider` class to deserialize JSON into `AnalyzeResult`.
  - Add a `UseMockData` flag in app settings to toggle mock vs. real OCR.
  - Test mock data compatibility with snippet parsing logic.
- **Success Criteria**: Mock data enables UI and hotkey development by Day 1, parsing 15-20 snippets correctly.

### 3.4 Snippet Parsing and Storage
- **Description**: Parse OCR output (or mock data) into snippets with unique IDs and store them in memory.
- **Requirements**:
  - Define `Snippet` class: `Id` (int), `Text` (string), `BoundingPolygon` (List<PointF>), `PageNumber` (int), `Confidence` (float).
  - Prioritize key-value pairs, fall back to lines for unstructured text.
  - Assign sequential IDs (1-20) to snippets, filtering out headers/footers with basic heuristics (e.g., skip all-uppercase short text).
  - Use `ConcurrentDictionary<int, Snippet>` for thread-safe storage.
  - Support updating snippets if edited and resetting on new PDF upload.
- **Success Criteria**: Stores 15-20 snippets from a 2-3 page PDF, accessible by ID for UI and hotkey use.

### 3.5 PDF Viewer with Identifier Overlays
- **Description**: Display the PDF with numbered overlays (e.g., “3”) over extracted snippets for visual reference.
- **Requirements**:
  - Use PdfiumViewer or MoonPdfLib to render PDF pages in a WPF scrollable viewer.
  - Calculate rendering scale based on PDF dimensions and DPI.
  - Overlay non-interactive labels (semi-transparent, red) at bounding polygon centroids.
  - Support zoom and multi-page navigation, rendering overlays only for visible pages.
  - Style overlays (font size, color, opacity) via app resources.
- **Success Criteria**: Displays a 2-3 page PDF with 15-20 numbered overlays, correctly positioned and zoomable.

### 3.6 Global Hotkey Support
- **Description**: Allow users to paste snippets into the web portal using global hotkeys (Ctrl+1 to Ctrl+0).
- **Requirements**:
  - Register global hotkeys via P/Invoke (user32.dll, RegisterHotKey) for Ctrl+1 to Ctrl+0, mapping to snippet IDs 1-10.
  - Fall back to app-level bindings if global registration fails.
  - Unregister hotkeys on app shutdown.
  - Copy snippet text to clipboard and simulate Ctrl+V paste using SendKeys or InputSimulator.
  - Add 100ms delay between copy and paste for reliability.
- **Success Criteria**: Ctrl+3 pastes snippet #3 into a focused web portal field, working across applications.

### 3.7 Snippet Review and Editing UI
- **Description**: Provide a sidebar to review and edit extracted snippets, ensuring user validation.
- **Requirements**:
  - Create a WPF ListView/DataGrid showing snippet ID, Text, and Confidence.
  - Allow editing of snippet text, syncing changes to storage and overlays.
  - Highlight low-confidence snippets (<0.8) in red via a “Validate All” button.
  - Add tooltips with page number and polygon data.
  - Include a search box for filtering snippets by text.
  - Display a summary panel (total snippets, average confidence).
- **Success Criteria**: Users can review 15-20 snippets, edit text, and filter by keyword.

### 3.8 Feedback Notifications
- **Description**: Notify users of key actions (OCR completion, hotkey paste, errors) via toasts.
- **Requirements**:
  - Use WPF popups or ToastNotification for messages (e.g., “PDF Analyzed: 20 snippets”).
  - Show errors (e.g., “Invalid PDF”) with retry options.
  - Confirm hotkey actions (e.g., “Pasted snippet #3”).
  - Log notifications to a file for debugging.
  - Allow disabling notifications via app settings.
- **Success Criteria**: Notifications appear for OCR, hotkey, and error events, logged for debugging.

### 3.9 Error Handling and Edge Cases
- **Description**: Handle failures gracefully to ensure a stable prototype.
- **Requirements**:
  - Display messages for empty OCR results, disabling hotkeys.
  - Limit processing to 13 pages, warning if exceeded.
  - Allow manual snippet addition for low-confidence OCR results.
  - Handle hotkey exceptions to prevent crashes.
  - Set 60-second timeout for Azure API calls.
- **Success Criteria**: App remains stable with invalid PDFs, no snippets, or API failures.

### 3.10 Performance Optimization
- **Description**: Optimize for low latency and resource usage within the hackathon scope.
- **Requirements**:
  - Cap snippets at 50, warning if exceeded.
  - Use async/await for non-blocking OCR and rendering.
  - Avoid persistent storage, keeping data in memory.
  - Batch overlay rendering for large PDFs.
- **Success Criteria**: Processes a 13-page PDF in <10 seconds, with smooth UI response.

### 3.11 Testing
- **Description**: Ensure end-to-end functionality via automated and manual tests.
- **Requirements**:
  - Test PDF upload and OCR (real or mock) for 15-20 snippets.
  - Verify hotkey pasting into a browser form.
  - Add a UI button to run tests, showing pass/fail results.
  - Document manual test scenarios (upload, edit, paste).
- **Success Criteria**: Tests pass for upload, extraction, and pasting, with a demo-ready flow.

## 4. Non-Functional Requirements

### 4.1 Performance
- OCR processing: <10 seconds for a 13-page PDF (Azure or mock).
- Hotkey paste: <500ms from trigger to paste.
- UI rendering: <2 seconds for PDF page with 20 overlays.

### 4.2 Usability
- Intuitive WPF interface with clear upload, review, and paste workflows.
- Non-intrusive notifications that don’t disrupt focus on the web portal.
- Error messages in plain language (e.g., “Please upload a valid PDF”).

### 4.3 Security
- Store Azure credentials in app settings or environment variables, not hard-coded.
- Avoid storing sensitive PDF data beyond the session.
- Use Azure Key Vault for credentials in future iterations (post-hackathon).

### 4.4 Compatibility
- Run on Windows 10/11 with .NET 8.0.
- Integrate with any browser-based web portal accepting clipboard paste.
- Support PDFs up to 50MB, adhering to Azure’s free tier limits (4MB for F0).

## 5. Technical Requirements

### 5.1 Tech Stack
- **Language**: C# with .NET 8.0 (Long-term support).
- **UI Framework**: WPF for desktop application.
- **OCR Service**: Azure Document Intelligence (v4.0, prebuilt-layout model, `features=keyValuePairs`).
- **PDF Rendering**: PdfiumViewer or MoonPdfLib (NuGet packages).
- **Dependencies**:
  - `Azure.AI.DocumentIntelligence` (v1.0.0-beta or latest compatible).
  - `System.Text.Json` for mock data deserialization.
  - `InputSimulator` or `SendKeys` for clipboard paste simulation.
- **Mock Data**: JSON file (`mock_ocr_output.json`) simulating `AnalyzeResult`.

### 5.2 Development Tools
- Visual Studio 2022 for coding and debugging.
- Azure Portal for Document Intelligence resource setup (free tier F0).
- Git for version control and collaboration.

### 5.3 Azure Setup
- Create a Document Intelligence resource in Azure Portal (F0 tier, 500 pages/month).
- Obtain endpoint and key, stored securely in app settings.
- Use `prebuilt-layout` model with `features=keyValuePairs` for extraction.

## 6. Assumptions and Constraints

### 6.1 Assumptions
- Developers are familiar with C#, .NET, and WPF.
- Azure Document Intelligence free tier (F0) is sufficient for hackathon testing.
- Web portal accepts standard clipboard paste (Ctrl+V) without restrictions.
- PDFs are text-based, not scanned images, for reliable OCR.
- Users have Windows 10/11 and .NET 8.0 installed.

### 6.2 Constraints
- **Timeline**: 3 days, ~24 hours per developer (72 hours total).
- **Team**: Three developers, no dedicated QA or designer.
- **Scope**: Limited to 13-page PDFs, 50 snippets, 10 hotkeys (Ctrl+1 to Ctrl+0).
- **Azure Limits**: Free tier (4MB PDFs, 500 pages/month).
- **No Backend**: No database writes, relying on front-end validations.

## 7. Success Metrics
- **Demo Success**: Upload a 2-3 page PDF, extract 15-20 snippets, display overlays, and paste 5 snippets into a web form via hotkeys during the hackathon demo.
- **User Efficiency**: Reduce data entry time by 50% compared to manual copy-paste (tested manually).
- **Stability**: No crashes during demo, with graceful error handling for invalid PDFs or failed OCR.
- **Usability**: Users can validate and edit snippets in <1 minute per PDF.

## 8. Risks and Mitigation
- **Risk**: Azure OCR setup delays (credentials, quota issues).
  - **Mitigation**: Use mock JSON data (`mock_ocr_output.json`) to unblock UI and hotkey development by Day 1.
- **Risk**: Poor OCR accuracy for complex PDFs.
  - **Mitigation**: Allow manual snippet editing in the UI; use high-quality sample PDFs for demo.
- **Risk**: Global hotkey registration fails on some systems.
  - **Mitigation**: Implement app-level key bindings as a fallback.
- **Risk**: Tight timeline for integration and testing.
  - **Mitigation**: Prioritize Tasks 1-3 (mock data) and 5 (OCR) on Day 1, allocate Day 3 for testing.

## 9. Deliverables
- **WPF Application**: Executable desktop app with PDF upload, viewer, snippet review, and hotkey paste functionality.
- **Mock Dataset**: `mock_ocr_output.json` with 2-3 pages, 15-20 snippets.
- **Test Suite**: Automated tests for upload, extraction, and pasting, plus manual test scenarios.
- **Demo Script**: 5-minute demo showing upload, extraction, overlay display, and hotkey pasting into a browser form.

## 10. Timeline
- **Day 1**: Mock data (Tasks 1-3), PDF upload (Task 4), basic OCR (Task 5), hotkey setup (Task 9), basic viewer (Task 7 partial).
- **Day 2**: Snippet parsing/storage (Tasks 6, 8), complete viewer (Task 7), hotkey integration (Task 10), notifications (Task 12), basic testing (Task 15 partial).
- **Day 3**: UI polish (Task 11), error handling (Task 13), optimization (Task 14), full testing (Task 15), demo prep.

## 11. Future Enhancements (Post-Hackathon)
- Support for scanned PDFs using Azure’s OCR for images.
- Advanced snippet filtering with machine learning.
- Persistent storage of snippets in a local database.
- Integration with web portal APIs for automated field mapping.
- Cross-platform support (e.g., web or macOS app).