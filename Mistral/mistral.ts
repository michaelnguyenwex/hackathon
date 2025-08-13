import "dotenv/config";
import { Mistral } from "@mistralai/mistralai";
import * as fs from "fs";
import * as path from "path";
import 'dotenv/config';

async function main(): Promise<void> {
	const apiKey = process.env.MISTRAL_API_KEY;
	if (!apiKey || apiKey.trim().length === 0) {
		console.error("Error: MISTRAL_API_KEY environment variable is not set.");
		process.exit(1);
	}

	const inputArgPath = process.argv[2];
	const defaultPdfPath = path.resolve(process.cwd(), "documents", "Test Design guide.pdf");
	const pdfPath = inputArgPath ? path.resolve(process.cwd(), inputArgPath) : defaultPdfPath;

	if (!fs.existsSync(pdfPath)) {
		console.error(`Error: File not found at path: ${pdfPath}`);
		process.exit(1);
	}

	console.log(`Reading PDF: ${pdfPath}`);
	const fileBuffer = fs.readFileSync(pdfPath);

	const client = new Mistral({ apiKey });

	try {
		console.log("Uploading PDF to Mistral for OCR...");
		const uploadedPdf = await client.files.upload({
			file: {
				fileName: path.basename(pdfPath),
				content: fileBuffer,
			},
			purpose: "ocr",
		});

		console.log("Getting signed URL for uploaded file...");
		const signedUrl = await client.files.getSignedUrl({
			fileId: uploadedPdf.id,
		});

		console.log("Running OCR process...");
		const ocrResponse = await client.ocr.process({
			model: "mistral-ocr-latest",
			document: {
				type: "document_url",
				documentUrl: signedUrl.url,
			},
			includeImageBase64: true,
		});

		const outputDir = path.resolve(process.cwd(), "Mistral");
		if (!fs.existsSync(outputDir)) {
			fs.mkdirSync(outputDir, { recursive: true });
		}
		const outputPath = path.join(outputDir, "ocr_output.json");
		fs.writeFileSync(outputPath, JSON.stringify(ocrResponse, null, 2), "utf-8");
		console.log(`OCR output saved to: ${outputPath}`);

		try {
			const pages: any[] = (ocrResponse as any).pages || [];
			const markdownParts: string[] = [];
			for (const page of pages) {
				if (page && typeof page.markdown === "string" && page.markdown.length > 0) {
					markdownParts.push(page.markdown);
				}
			}
			if (markdownParts.length > 0) {
				const mdOutputPath = path.join(outputDir, "ocr_output.md");
				fs.writeFileSync(mdOutputPath, markdownParts.join("\n\n"), "utf-8");
				console.log(`OCR markdown saved to: ${mdOutputPath}`);
			}
		} catch {
			// noop: markdown extraction is best-effort
		}
	} catch (error) {
		console.error("OCR failed:", error);
		process.exit(1);
	}
}

void main();


