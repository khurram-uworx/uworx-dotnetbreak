﻿using System.Drawing;
using Microsoft.ML;
using ML.Onnx.YoloParser;
using ML.Onnx;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing.Drawing2D;


var assetsRelativePath = @"../../../../../data";
string assetsPath = GetAbsolutePath(assetsRelativePath);
var modelFilePath = Path.Combine(assetsPath, "tinyyolov2-7.onnx");
var imagesFolder = assetsPath;
var outputFolder = Path.Combine(assetsPath, "output");

// Initialize MLContext
MLContext mlContext = new MLContext();

try
{
    // Load Data
    IEnumerable<ImageNetData> images = ImageNetData.ReadFromFile(imagesFolder);
    IDataView imageDataView = mlContext.Data.LoadFromEnumerable(images);

    // Create instance of model scorer
    var modelScorer = new OnnxModelScorer(imagesFolder, modelFilePath, mlContext);

    // Use model to score data
    IEnumerable<float[]> probabilities = modelScorer.Score(imageDataView);

    // Post-process model output
    YoloOutputParser parser = new YoloOutputParser();

    var boundingBoxes =
        probabilities
        .Select(probability => parser.ParseOutputs(probability))
        .Select(boxes => parser.FilterBoundingBoxes(boxes, 5, .5F));

    // Draw bounding boxes for detected objects in each of the images
    for (var i = 0; i < images.Count(); i++)
    {
        string imageFileName = images.ElementAt(i).Label;
        IList<YoloBoundingBox> detectedObjects = boundingBoxes.ElementAt(i);

        DrawBoundingBox(imagesFolder, outputFolder, imageFileName, detectedObjects);

        LogDetectedObjects(imageFileName, detectedObjects);
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}

Console.WriteLine("========= End of Process..Hit any Key ========");

string GetAbsolutePath(string relativePath)
{
    FileInfo _dataRoot = new FileInfo(typeof(Program).Assembly.Location);
    string assemblyFolderPath = _dataRoot.Directory.FullName;

    string fullPath = Path.Combine(assemblyFolderPath, relativePath);

    return fullPath;
}

void DrawBoundingBox(string inputImageLocation, string outputImageLocation, string imageName, IList<YoloBoundingBox> filteredBoundingBoxes)
{
    System.Drawing.Image image = System.Drawing.Image.FromFile(Path.Combine(inputImageLocation, imageName));

    var originalImageHeight = image.Height;
    var originalImageWidth = image.Width;

    foreach (var box in filteredBoundingBoxes)
    {
        // Get Bounding Box Dimensions
        var x = (uint)Math.Max(box.Dimensions.X, 0);
        var y = (uint)Math.Max(box.Dimensions.Y, 0);
        var width = (uint)Math.Min(originalImageWidth - x, box.Dimensions.Width);
        var height = (uint)Math.Min(originalImageHeight - y, box.Dimensions.Height);

        // Resize To Image
        x = (uint)originalImageWidth * x / OnnxModelScorer.ImageNetSettings.imageWidth;
        y = (uint)originalImageHeight * y / OnnxModelScorer.ImageNetSettings.imageHeight;
        width = (uint)originalImageWidth * width / OnnxModelScorer.ImageNetSettings.imageWidth;
        height = (uint)originalImageHeight * height / OnnxModelScorer.ImageNetSettings.imageHeight;

        // Bounding Box Text
        string text = $"{box.Label} ({(box.Confidence * 100).ToString("0")}%)";

        using (Graphics thumbnailGraphic = Graphics.FromImage(image))
        {
            thumbnailGraphic.CompositingQuality = CompositingQuality.HighQuality;
            thumbnailGraphic.SmoothingMode = SmoothingMode.HighQuality;
            thumbnailGraphic.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Define Text Options
            System.Drawing.Font drawFont = new System.Drawing.Font("Arial", 12, FontStyle.Bold);
            SizeF size = thumbnailGraphic.MeasureString(text, drawFont);
            SolidBrush fontBrush = new SolidBrush(Color.Black);
            Point atPoint = new Point((int)x, (int)y - (int)size.Height - 1);

            // Define BoundingBox options
            Pen pen = new Pen(box.BoxColor, 3.2f);
            SolidBrush colorBrush = new SolidBrush(box.BoxColor);

            // Draw text on image 
            thumbnailGraphic.FillRectangle(colorBrush, (int)x, (int)(y - size.Height - 1), (int)size.Width, (int)size.Height);
            thumbnailGraphic.DrawString(text, drawFont, fontBrush, atPoint);

            // Draw bounding box on image
            thumbnailGraphic.DrawRectangle(pen, x, y, width, height);
        }
    }

    if (!Directory.Exists(outputImageLocation))
    {
        Directory.CreateDirectory(outputImageLocation);
    }

    image.Save(Path.Combine(outputImageLocation, imageName));
}

void LogDetectedObjects(string imageName, IList<YoloBoundingBox> boundingBoxes)
{
    Console.WriteLine($".....The objects in the image {imageName} are detected as below....");

    foreach (var box in boundingBoxes)
    {
        Console.WriteLine($"{box.Label} and its Confidence score: {box.Confidence}");
    }

    Console.WriteLine("");
}
