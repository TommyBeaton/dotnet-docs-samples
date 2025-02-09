// See https://aka.ms/new-console-template for more information

using GeminiTesting;
using GeminiTesting.Interfaces;

IModelFunction func = new OCRDocument();

await func.Execute();
