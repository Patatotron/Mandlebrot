using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SkiaSharp;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Spectre.Console;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.MapGet("/mandelbrot", async context =>
        {
            await new MandelbrotRenderer().RenderMandelbrot(context);
        });

        app.Run();
    }
}

public class MandelbrotRenderer
{
    public async Task RenderMandelbrot(HttpContext context)
    {
        int width = 1080;
        int height = 1080;


        double zoom = 1;
        double moveX = -0.7448617666197486;  // Center X coordinate
        double moveY = -0.1225611668766516;  // Center Y coordinate

        // Start with a base number of iterations for a standard zoom level (like zoom = 1)
        int baseIterations = 1000;  

        // Adjust the number of iterations based on zoom
        // As you zoom in, we want more iterations to get more precision, but not linearly
        int zoomFactor = (int)(Math.Log(zoom + 1) * 1000);  // The zoom impact
        int scaleFactor = (int)(Math.Sqrt(width * height) / 1000);  // Adjust based on image resolution

        int maxIterations = baseIterations + zoomFactor + scaleFactor;

        // Cap max iterations at a reasonable upper bound, for example, 100,000 iterations
        maxIterations = Math.Min(maxIterations, 100000);

        // Ensure a minimum number of iterations to avoid too few iterations at low zoom levels
        maxIterations = Math.Max(maxIterations, 500); 

        // Adjust scaling based on zoom
        double scaleX = 4.0 / width / zoom;   // Horizontal scaling
        double scaleY = 4.0 / height / zoom;  // Vertical scaling

        string outputDir = "frames"; // Directory to save frames
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        for (int frame = 0; frame < 1000000000; frame++)  // Change the frame count as needed
        {
            string filePath = Path.Combine(outputDir, $"frame_{frame:D4}.png");

            using var bitmap = new SKBitmap(width, height);
            var pixelData = new SKColor[width * height];

            var progressLock = new object();

            AnsiConsole.Progress()
                .Start(ctx =>
                {
                    var task = ctx.AddTask("[green]Rendering Mandelbrot[/]");

                    int completedLines = 0;
                    Parallel.ForEach(Partitioner.Create(0, height), range =>
                    {
                        for (int y = range.Item1; y < range.Item2; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                // Map pixel to the complex plane
                                double c_re = (x - width / 2.0) * scaleX + moveX;
                                double c_im = (y - height / 2.0) * scaleY + moveY;

                                double z_re = c_re, z_im = c_im;
                                int i;
                                for (i = 0; i < maxIterations; i++)
                                {
                                    if (z_re * z_re + z_im * z_im > 4)
                                        break;
                                    double new_re = z_re * z_re - z_im * z_im + c_re;
                                    double new_im = 2 * z_re * z_im + c_im;
                                    z_re = new_re;
                                    z_im = new_im;
                                }

                                if (i == maxIterations)
                                {
                                    pixelData[y * width + x] = SKColors.Black;
                                }
                                else
                                {
                                    float hue = (float)(i % 256) / 256f * 360;
                                    pixelData[y * width + x] = SKColor.FromHsl(hue, 100, 50);
                                }
                            }

                            lock (progressLock)
                            {
                                completedLines++;
                                task.Value = (completedLines * 100.0) / height;
                            }
                        }
                    });
                });

            bitmap.Pixels = pixelData;

            SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                data.SaveTo(fileStream);
                Console.WriteLine($"Saved frame {frame:D4} to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving frame {frame:D4}: {ex.Message}");
            }

            // Update zoom for the next frame (simulate zooming in)
            zoom *= 1.05;  // Adjust zoom increment as needed
            scaleX = 4.0 / width / zoom;
            scaleY = 4.0 / height / zoom;
        }

        Console.WriteLine("Finished rendering frames!");
    }
}