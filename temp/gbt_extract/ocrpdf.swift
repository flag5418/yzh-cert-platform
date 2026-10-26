import Foundation
import PDFKit
import Vision
import CoreGraphics
import AppKit

// Usage: ocrpdf <input.pdf> <output.txt> [scale]
let args = CommandLine.arguments
guard args.count >= 3 else {
    FileHandle.standardError.write("usage: ocrpdf <in.pdf> <out.txt> [scale]\n".data(using: .utf8)!)
    exit(2)
}
let inPath = args[1]
let outPath = args[2]
let scale: CGFloat = args.count > 3 ? CGFloat(Double(args[3]) ?? 2.0) : 2.0

guard let doc = PDFDocument(url: URL(fileURLWithPath: inPath)) else {
    FileHandle.standardError.write("cannot open pdf\n".data(using: .utf8)!)
    exit(3)
}

var out = ""
for i in 0..<doc.pageCount {
    guard let page = doc.page(at: i) else { continue }
    let bounds = page.bounds(for: .mediaBox)
    let w = Int(bounds.width * scale)
    let h = Int(bounds.height * scale)
    guard w > 0, h > 0 else { continue }
    let cs = CGColorSpaceCreateDeviceRGB()
    guard let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8,
                              bytesPerRow: 0, space: cs,
                              bitmapInfo: CGImageAlphaInfo.premultipliedFirst.rawValue) else { continue }
    ctx.setFillColor(CGColor(red: 1, green: 1, blue: 1, alpha: 1))
    ctx.fill(CGRect(x: 0, y: 0, width: w, height: h))
    ctx.scaleBy(x: scale, y: scale)
    page.draw(with: .mediaBox, to: ctx)
    guard let img = ctx.makeImage() else { continue }

    let req = VNRecognizeTextRequest()
    req.recognitionLevel = .accurate
    req.recognitionLanguages = ["zh-Hans", "en-US"]
    req.usesLanguageCorrection = true
    let handler = VNImageRequestHandler(cgImage: img, options: [:])
    do {
        try handler.perform([req])
    } catch {
        FileHandle.standardError.write("ocr fail page \(i+1)\n".data(using: .utf8)!)
        continue
    }
    var lines: [(CGFloat, CGFloat, String)] = []
    if let obs = req.results {
        for o in obs {
            guard let cand = o.topCandidates(1).first else { continue }
            let bb = o.boundingBox
            lines.append((1.0 - bb.origin.y, bb.origin.x, cand.string))
        }
    }
    // sort top-to-bottom, then left-to-right
    lines.sort { a, b in
        if abs(a.0 - b.0) < 0.006 { return a.1 < b.1 }
        return a.0 < b.0
    }
    out += "\n===PAGE \(i+1)===\n"
    for l in lines { out += l.2 + "\n" }
    FileHandle.standardError.write("page \(i+1)/\(doc.pageCount)\n".data(using: .utf8)!)
}

try? out.write(toFile: outPath, atomically: true, encoding: .utf8)
print("done \(doc.pageCount) pages -> \(outPath)")
