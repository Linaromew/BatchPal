using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BatchPaletter
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            Task.Run(() => PaletteHandlerTask(files));
        }

        private void PaletteHandlerTask(string[] files)
        {
            List<System.Drawing.Color> sortedColours = ScrapeColoursFromImages(files);

            string tempPalettePath = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".png";
            SavePaletteOutput(sortedColours, tempPalettePath);

            OpenPaletteForEditing(tempPalettePath);
            MessageBox.Show("Please edit the palette as desired, and select 'OK' when complete.");
            ProcessFilesWithEditedPalette(files, tempPalettePath);
        }

        private void OpenPaletteForEditing(string filePath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(filePath)
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open the palette for editing: {ex.Message}");
            }
        }

        private void ProcessFilesWithEditedPalette(string[] files, string editedPalettePath)
        {
            List<string> filesToProcess = new();

            foreach (string file in files)
            {
                if (System.IO.Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                    !file.Equals(editedPalettePath, StringComparison.OrdinalIgnoreCase))
                {
                    filesToProcess.Add(file);
                }
            }

            if (!string.IsNullOrEmpty(editedPalettePath))
            {
                BatchPaletteSwap(filesToProcess, editedPalettePath);
            }
            else
            {
                MessageBox.Show("No palette file was found or selected for processing.");
            }
        }

        private static void BatchPaletteSwap(List<string> filesToProcess, string paletteFile)
        {
            string outputString = Guid.NewGuid().ToString();

            // Extract palette colors
            using Bitmap paletteBitmap = new(paletteFile);
            System.Drawing.Color[] originalColours = new System.Drawing.Color[paletteBitmap.Width];
            System.Drawing.Color[] newColours = new System.Drawing.Color[paletteBitmap.Width];

            for (int x = 0; x < paletteBitmap.Width; x++)
            {
                originalColours[x] = paletteBitmap.GetPixel(x, 0);
                newColours[x] = paletteBitmap.GetPixel(x, 1);
            }

            foreach (string file in filesToProcess)
            {
                if (!file.Equals(paletteFile, StringComparison.OrdinalIgnoreCase))
                {
                    using Bitmap originalImage = new(file);
                    using Bitmap tempImage = new(originalImage.Width, originalImage.Height);
                    bool modified = false;

                    for (int y = 0; y < originalImage.Height; y++)
                    {
                        for (int x = 0; x < originalImage.Width; x++)
                        {
                            System.Drawing.Color pixelColour = originalImage.GetPixel(x, y);
                            int index = Array.IndexOf(originalColours, pixelColour);

                            if (index != -1)
                            {
                                tempImage.SetPixel(x, y, newColours[index]);
                                modified = true;
                            }
                            else
                            {
                                tempImage.SetPixel(x, y, pixelColour);
                            }
                        }
                    }

                    if (modified)
                    {
                        originalImage.Dispose();

                        try
                        {
                            string outputDirectoryName = "Output_" + outputString;
                            string fileDirectory = Path.GetDirectoryName(file);
                            string outputDirectoryPath = Path.Combine(fileDirectory, outputDirectoryName);
                            Directory.CreateDirectory(outputDirectoryPath);

                            string outputFilePath = Path.Combine(outputDirectoryPath, Path.GetFileNameWithoutExtension(file) + ".png");

                            tempImage.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
        }

        private List<System.Drawing.Color> ScrapeColoursFromImages(string[] files)
        {
            HashSet<System.Drawing.Color> uniqueColours = [];

            foreach (string file in files)
            {
                if (Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Bitmap bitmap = new Bitmap(file);
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            for (int y = 0; y < bitmap.Height; y++)
                            {
                                System.Drawing.Color pixelColor = bitmap.GetPixel(x, y);
                                Console.WriteLine(pixelColor.ToArgb().ToString());
                                uniqueColours.Add(pixelColor);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error processing file {file}: {ex.Message}");
                    }
                }
            }

            //MessageBox.Show($"Found {uniqueColours.Count} unique colours in {files.Length} images!");
            return SortByColourDistance(uniqueColours);
        }

        public static List<System.Drawing.Color> SortByColourDistance(HashSet<System.Drawing.Color> uniqueColours)
        {
            /* Thank you LINQ oh my god */
            var sortedColours = uniqueColours.OrderBy(c => c.GetHue())
                               .ThenBy(c => c.GetSaturation())
                               .ThenBy(c => c.GetBrightness())
                               .ThenBy(c => c.GetHue() + c.GetSaturation() + c.GetBrightness())
                               .ToList();

            bool swapped;
            do
            {
                swapped = false;
                for (int i = 0; i < uniqueColours.Count - 1; i++)
                {
                    for (int j = i + 1; j < uniqueColours.Count; j++)
                    {
                        if (ColourDistance(sortedColours[i], sortedColours[i + 1]) > ColourDistance(sortedColours[i], sortedColours[j]))
                        {
                            (sortedColours[j], sortedColours[i + 1]) = (sortedColours[i + 1], sortedColours[j]);
                            swapped = true;
                        }
                    }
                }
            } while (swapped);

            return sortedColours;
        }

        public static double ColourDistance(System.Drawing.Color e1, System.Drawing.Color e2)
        {
            long rmean = ((long)e1.R + (long)e2.R) / 2;
            long r = (long)e1.R - (long)e2.R;
            long g = (long)e1.G - (long)e2.G;
            long b = (long)e1.B - (long)e2.B;
            return Math.Sqrt((((512 + rmean) * r * r) >> 8) + 4 * g * g + (((767 - rmean) * b * b) >> 8));
        }

        private static void SavePaletteOutput(List<System.Drawing.Color> sortedColours, string saveFileName)
        {
            int width = sortedColours.Count;
            int height = 2;
            WriteableBitmap writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            byte[] colourData = new byte[width * height * 4];
            int index = 0;
            foreach (var sdColour in sortedColours)
            {
                var colour = System.Windows.Media.Color.FromArgb(sdColour.A, sdColour.R, sdColour.G, sdColour.B);

                colourData[index++] = colour.B;
                colourData[index++] = colour.G;
                colourData[index++] = colour.R;
                colourData[index++] = colour.A;
            }

            Array.Copy(colourData, 0, colourData, width * 4, width * 4);

            writeableBitmap.WritePixels(new Int32Rect(0, 0, width, height), colourData, width * 4, 0);

            using (FileStream fileStream = new FileStream(saveFileName, FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                encoder.Save(fileStream);
            }

            Console.WriteLine($"Palette saved to: {saveFileName}");
        }
    }
}