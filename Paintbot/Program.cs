﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Paintbot
//everything in absolute coordinates (G53)

//TODO: test g-code simulator
//TODO: Test different brush sizes
{
    class Program
    {
        public static float oldXpos = 0;
        public static float oldYpos = 0;
        public static float oldZpos = 0;
        public static Form1 form1;
        public static Bitmap image1;
        public static HashSet<ColorDef> colorPalette;


        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            form1 = new Form1();
            LoadImage();
            DisplayPictureSize();
            RefreshPreview();
            ParseColors();

            Application.Run(form1); 
        }

        public static void GenerateGCode()
        {
            Cursor.Current = Cursors.WaitCursor;

            float brushSize = (float)Settings.Default.brushsize_mm;
            float zMoveHeight = (float)Settings.Default.zMoveHeight_mm;
            float colorPositionX = (float)Settings.Default.colorPosX_mm;
            float colorPositionY = (float)Settings.Default.colorPosY_mm;
            float colorPositionZ = (float)Settings.Default.colorPosZ_mm;
            float colorContainerHeight = (float)Settings.Default.colorContainerHeight_mm;
            float zMoveDepth = (float)Settings.Default.zDepth_mm;
            int pickColorFrequency = (int)Settings.Default.pickColorFrequency;
            int zSpeed = (int)Settings.Default.zSpeed_mm_min;
            int xySpeed = (int)Settings.Default.xySpeed_mm_min;
            String ignoreColor = Settings.Default.ignoreColor_hex;
            String imagePath = Settings.Default.imagePath;
            String outputPath = Settings.Default.outputPath;
            String gcodeStartPath = Settings.Default.gcodeStartPath;
            String gcodeEndPath = Settings.Default.gcodeEndPath;
            bool colorAtYgantry = Settings.Default.colorAtYgantry;
            bool flipYAxis = Settings.Default.flipYAxis;
            bool endOverWater = Settings.Default.endOverWater;
            bool moveXZsameTime = Settings.Default.moveZXsameTime;
            float canvasZeroPosX_mm = (float)Settings.Default.canvasZeroPosX_mm;
            float canvasZeroPosY_mm = (float)Settings.Default.canvasZeroPosY_mm;
            float canvasZeroPosZ_mm = (float)Settings.Default.canvasZeroPosZ_mm;
            int progressBar = Settings.Default.progressbar; //value between 0 and 100
            bool useColorPosDef = Settings.Default.useColorPosDef;
            int maxNumColorPerFile = (int)Settings.Default.maxNumColorPerFile;

            Directory.CreateDirectory(outputPath);
            DirectoryInfo di = new DirectoryInfo(outputPath);

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }

            string gcodeStart = System.IO.File.ReadAllText(gcodeStartPath);
            string gcodeEnd = System.IO.File.ReadAllText(gcodeEndPath);

            int x, y;

            HashSet<string> colorStrings = GetColorStrings(ignoreColor);

            int progress = 0;
            string[,] pathArray = new string[colorStrings.Count,2];
            int pathNo = 0;

            foreach (string colorType in colorStrings)
            {
                float progressRatio = (float)progress / (float)colorStrings.Count;
                progressBar = (int)(progressRatio * 100);
                progress++;
                form1.progressBar1.Value = progressBar;
                form1.progressBar1.Update();

                //ensure paths exist
                System.IO.Directory.CreateDirectory(outputPath);
                System.IO.Directory.CreateDirectory(outputPath + "\\singleColors");

                string gcodePath = Path.Combine(outputPath + "\\singleColors", AztecColorAssign(colorType) + ".gcode");
                StreamWriter fileOut = new StreamWriter(gcodePath, true);
                pathArray[pathNo, 0] = gcodePath;
                pathArray[pathNo, 1] = AztecColorAssign(colorType);
                pathNo++;

                if(maxNumColorPerFile == 1)
                {
                    fileOut.WriteLine(gcodeStart);
                }

                HashSet<ColorCoordinate> colorCoordinates = new HashSet<ColorCoordinate>();
                for (x = 0; x < image1.Width; x++)  // Loop through the images pixels
                {
                    for (y = 0; y < image1.Height; y++)
                    {
                        System.Drawing.Color pixelColor = image1.GetPixel(x, y);
                        {
                            if (string.Equals(pixelColor.Name, colorType))
                            {
                                colorCoordinates.Add(new ColorCoordinate(x, y));
                            }
                        }
                    }
                }

                if (useColorPosDef)//automatic get color from position
                {
                    colorAtYgantry = false; //to ensure color at gantry not selected when positions are used
                    foreach (ColorDef colorDef in colorPalette)
                    {
                        if (colorDef.ColorHex.Equals(colorType))
                        {
                            colorPositionX = colorDef.XPos;
                            colorPositionY = colorDef.YPos;
                            break;
                        }
                    }
                }

                int pixelNo = 0;
                int getColor = 0;
                ColorCoordinate currentPoint = null;
                ColorCoordinate nextPoint = null;
                HashSet<ColorCoordinate> doneColorCoordinates = new HashSet<ColorCoordinate>();

                foreach (ColorCoordinate point in colorCoordinates)
                {
                    if (currentPoint == null)
                    {   //initialization of the first point
                        currentPoint = point;
                        nextPoint = point;
                        doneColorCoordinates.Add(point);
                    }
                    
                    
                    foreach (ColorCoordinate nPoint in colorCoordinates)
                    {
                        if (!doneColorCoordinates.Contains(nPoint))
                        {
                            if (nextPoint.Equals(currentPoint) && !nPoint.Equals(currentPoint))
                            {
                                nextPoint = nPoint;
                            }
                            if (currentPoint.checkDistance(nPoint) < currentPoint.checkDistance(nextPoint))
                            {
                                nextPoint = nPoint;
                            }
                        }
                    }

                    if (pixelNo % pickColorFrequency == 0)
                    {
                        if ((getColor+1) % Settings.Default.cleanBrushPicks == 0)
                        {
                            fileOut.WriteLine(CleanBrush(Settings.Default.useWater, false, false, colorPositionZ, zSpeed, xySpeed));
                        }
                        fileOut.WriteLine(GetColor(pixelNo, colorPositionX, colorPositionY, colorAtYgantry, colorPositionZ, colorContainerHeight, zSpeed, xySpeed, moveXZsameTime, (float)Settings.Default.colorMoveRadius, false));
                        getColor++;
                    }

                    pixelNo++;

                    if (flipYAxis)
                    {
                        fileOut.WriteLine(PaintStroke(currentPoint.getXpos(), -currentPoint.getYpos(), zMoveHeight, zMoveDepth, brushSize, zSpeed, xySpeed, canvasZeroPosX_mm, canvasZeroPosY_mm, canvasZeroPosZ_mm));
                    }
                    else
                    {
                        fileOut.WriteLine(PaintStroke(currentPoint.getXpos(), currentPoint.getYpos(), zMoveHeight, zMoveDepth, brushSize, zSpeed, xySpeed, canvasZeroPosX_mm, canvasZeroPosY_mm, canvasZeroPosZ_mm));
                    }
                    
                    currentPoint = nextPoint;
                    doneColorCoordinates.Add(currentPoint);

                }

                if (endOverWater)
                {   //end with cleaning brush
                    fileOut.WriteLine(CleanBrush(Settings.Default.useWater, Settings.Default.useSponge, Settings.Default.useTissue, colorPositionZ, zSpeed, xySpeed));
                }
                else
                {
                    if (maxNumColorPerFile == 1)
                    {
                        fileOut.WriteLine(gcodeEnd);
                    }
                }

                fileOut.Close();
            }

            if(maxNumColorPerFile > 1)
            {
                int noOfFiles = pathArray.Length / 2;
                double numberOfRuns = Math.Ceiling((float)noOfFiles / maxNumColorPerFile);
                for(int k = 0; k < numberOfRuns; k++)
                {
                    int offset = k * maxNumColorPerFile;
                    
                    bool firstRun = true;
                    string fileName = Settings.Default.filePrefix + "_" + k;
                    string colors = "";

                    int upperlimit = offset + maxNumColorPerFile;
                    if(upperlimit > noOfFiles)
                    {
                        upperlimit = noOfFiles;
                    }

                    for (int i = offset; i < upperlimit; i++)
                    {
                        //generate colors as comment
                        if (firstRun)
                        {
                            colors = ";" + pathArray[i, 1].Substring(0, 4);
                            firstRun = false;
                        }
                        else
                        {
                            colors = colors + "_" + pathArray[i, 1].Substring(0, 4);
                        }
                    }
                    firstRun = true;
                    string filePath = outputPath + fileName + ".gcode";
                    for (int i = offset; i < upperlimit; i++)
                    {
                        if (firstRun)
                        {
                            File.AppendAllText(filePath, colors + "\n\n");
                            File.AppendAllText(filePath, gcodeStart);
                            File.AppendAllText(filePath, File.ReadAllText(pathArray[i, 0]));
                            firstRun = false;
                        }
                        else
                        {
                            File.AppendAllText(filePath, File.ReadAllText(pathArray[i, 0]));
                        }
                    }
                    File.AppendAllText(filePath, gcodeEnd);
                }
            }

            form1.progressBar1.Value = 100;
            form1.progressBar1.Update();

            Cursor.Current = Cursors.Default;

            Form2 formPopup = new Form2(form1);
            formPopup.StartPosition = FormStartPosition.CenterParent;
            formPopup.ShowDialog();
        }

        //TODO: work on circle drawings 
        //https://www.bigstockphoto.com/image-330726007/stock-vector-divide-math-operation-mosaic-of-round-dots-in-various-sizes-and-shades%2C-based-on-divide-math-operati
        public static void FillWithCircles(int minCircleDiameterPixel)
        {
            //idea: 
            //loop through pixels for colors and when color found loop to left and right check width, then to top and bottom check height, 
            //add both together and calculate center point, make a CirclePoint(int centerX, int centerY, int width, int height)
            //if next point has bigger Width+Height replace Circlepoint after looping through image draw a circle with smaller value of height and width around the centerpoint in circleImage. 
            //overwrite that area with ignorecolor rectangle in imageCopy. If after looping circle would be < then minCircleDiameterPixel overwrite with ignorcolor without drawing circle
            //repeat until color not found anymore -> next color

            HashSet<string> pictureColors = GetColorStrings(Settings.Default.ignoreColor_hex);
            Bitmap imageCopy = new Bitmap(image1);
            Bitmap circleImage = new Bitmap(image1);
        }

        static HashSet<string> GetColorStrings(string ignoreColor)
        {
            HashSet<string> colorStrings = new HashSet<string>();

            for (int x = 0; x < image1.Width; x++)  // Loop through the images pixels
            {
                for (int y = 0; y < image1.Height; y++)
                {
                    System.Drawing.Color pixelColor = image1.GetPixel(x, y);
                    {
                        if (!String.Equals(pixelColor.Name, ignoreColor))
                        {
                            colorStrings.Add(pixelColor.Name);
                        }
                    }
                }
            }

            return colorStrings;
        }

        static string GetColor(int getColorIndex, float xPos, float yPos, bool colorAtYgantry, float zPos, float zColorHeight, int zSpeed, int xySpeed, bool moveXZsameTime, float xyMoveRadius, bool brushClean)
        {
            /*
             * xPos = X position of the color
             * yPos = Y position of the color
             * zPos = Z position of the color
             * zColorHeight = height of the color container plus an offset to get over the edge
            */

            string getColorString = "";
            string gcodePartStart = "; get paint start";
            string gcodePartEnd = "; get paint end \n";
            if (brushClean)
            {
                gcodePartStart = "; brush clean start";
                gcodePartEnd = "; brush clean end \n";
            }


            if (moveXZsameTime)
            {
                double phi = Math.Atan(Math.Abs(zPos - oldZpos) / Math.Abs(xPos - oldXpos));
                double feedRate1 = zSpeed / Math.Sin(phi);
                double feedRate2 = xySpeed / Math.Cos(phi);

                double feedRate = 0.0;

                if(feedRate1 < feedRate2)
                {
                    feedRate = feedRate1;
                }
                else
                {
                    feedRate = feedRate2;
                }

                if(getColorIndex == 0)
                {
                    feedRate = zSpeed;
                }

                if (colorAtYgantry)
                {
                    getColorString = "\nG53 X" + xPos.ToString().Replace(',', '.') + " Z" + (zColorHeight + zPos) + " F" + (int)feedRate + gcodePartStart +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + gcodePartEnd;
                }
                else
                {
                    getColorString = "\nG53 X" + xPos.ToString().Replace(',', '.') + " Y" + yPos.ToString().Replace(',', '.') + " Z" + zColorHeight + " F" + (int)feedRate + gcodePartStart +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " Y" + yPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + zColorHeight + " F" + zSpeed + gcodePartEnd;
                }

            }
            else
            {
                if (colorAtYgantry)
                {
                    getColorString = "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + gcodePartStart +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + //" Y" + yPos.ToString().Replace(',', '.') + 
                    " F" + xySpeed +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + gcodePartEnd;
                }
                else
                {
                    getColorString = "\nG53 Z" + zColorHeight + " F" + zSpeed + gcodePartStart +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " Y" + yPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " Y" + yPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + zColorHeight + " F" + zSpeed + gcodePartEnd;
                }
            }

            oldXpos = xPos;
            oldYpos = yPos;
            oldZpos = zPos;

            return getColorString;
        }

        static string CleanBrush(bool useWater, bool useSponge, bool useTissue, float zColorHeight, int zSpeed, int xySpeed) 
        {
            //automatic brush cleaning
            string cleanBrush = "";
            //tissue
            if (useTissue)
            {
                cleanBrush = cleanBrush + GetColor(1, (float)Settings.Default.tissuePosX_mm, (float)Settings.Default.tissuePosY_mm, false, (float)Settings.Default.tissuePosZ_mm, zColorHeight, zSpeed, xySpeed, false, (float)Settings.Default.tissueMoveRadius, true);
            }
            //water
            if (useWater)
            {
                cleanBrush = cleanBrush + GetColor(1, (float)Settings.Default.waterPosX_mm, (float)Settings.Default.waterPosY_mm, false, (float)Settings.Default.waterPosZ_mm, zColorHeight, zSpeed, xySpeed, false, (float)Settings.Default.waterMoveRadius, true);
            }
            //sponge
            if (useSponge)
            {
                cleanBrush = cleanBrush + GetColor(1, (float)Settings.Default.spongePosX_mm, (float)Settings.Default.spongePosY_mm, false, (float)Settings.Default.spongePosZ_mm, zColorHeight, zSpeed, xySpeed, false, (float)Settings.Default.spongeMoveRadius, true);
            }
            //water
            if (useWater)
            {
                cleanBrush = cleanBrush + GetColor(1, (float)Settings.Default.waterPosX_mm, (float)Settings.Default.waterPosY_mm, false, (float)Settings.Default.waterPosZ_mm, zColorHeight, zSpeed, xySpeed, false, (float)Settings.Default.waterMoveRadius, true);
            }

            return cleanBrush;
        }

        static string PaintStroke(int xPos, int yPos, float zMoveHeight, float zMoveDepth, float brushSize, int zSpeed, int xySpeed, float canvasZeroPosX_mm, float canvasZeroPosY_mm, float canvasZeroPosZ_mm)
        {

            /*
             * xPos = X position of the color
             * yPos = Y position of the color
             * brushSize = brushsize in mm
             * offsetX = offset X in mm
             * offsetY = offset Y in mm
             * offsetZ = heigth of the canvas
             * zMoveHeight = z height for movement over canvas
            */
            if (Settings.Default.centerOnCanvas)
            {
                float width = (float)Settings.Default.maxWidthX / brushSize;
                float height = (float)Settings.Default.maxHeightY / brushSize;
                if (image1.Width < width && image1.Height >= height - 1)
                {
                    int xOffset = ((int)width - image1.Width) / 2;
                    canvasZeroPosX_mm = canvasZeroPosX_mm + xOffset;
                }
                else if (image1.Width >= width - 1 && image1.Height < height)
                {
                    int yOffset = ((int)height - image1.Height) / 2;
                    canvasZeroPosY_mm = canvasZeroPosY_mm + yOffset;
                }
            }

            string paintStrokeString = "";

            if (xPos == oldXpos)
            {
                paintStrokeString = "\nG53 Y" + (yPos * brushSize + canvasZeroPosY_mm).ToString().Replace(',', '.') + " F" + xySpeed +
                "\nG53 Z" + (zMoveDepth + canvasZeroPosZ_mm).ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                "\nG53 Z" + (zMoveHeight + canvasZeroPosZ_mm).ToString().Replace(',', '.') + " F" + zSpeed + "; paint stroke end";
            }
            else if(yPos == oldYpos)
            {
                paintStrokeString = "\nG53 X" + (xPos * brushSize + canvasZeroPosX_mm).ToString().Replace(',', '.') + " F" + xySpeed +
                "\nG53 Z" + (zMoveDepth + canvasZeroPosZ_mm).ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                "\nG53 Z" + (zMoveHeight + canvasZeroPosZ_mm).ToString().Replace(',', '.') + " F" + zSpeed + "; paint stroke end";
            }
            else
            {
                paintStrokeString = "\nG53 X" + (xPos * brushSize + canvasZeroPosX_mm).ToString().Replace(',', '.') + " Y" + (yPos * brushSize + canvasZeroPosY_mm).ToString().Replace(',', '.') + " F" + xySpeed +
                "\nG53 Z" + (zMoveDepth + canvasZeroPosZ_mm).ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                "\nG53 Z" + (zMoveHeight + canvasZeroPosZ_mm).ToString().Replace(',', '.') + " F" + zSpeed + "; paint stroke end";
            }

            oldXpos = xPos*brushSize + canvasZeroPosX_mm;
            oldYpos = yPos*brushSize + canvasZeroPosY_mm;
            oldZpos = zMoveHeight + canvasZeroPosZ_mm;

            return paintStrokeString;
        }

        public static void LoadImage()
        {
            String imagePath = Settings.Default.imagePath;
            image1 = new Bitmap(imagePath);
        }

        public static void ResizePicture()
        {
            float brushSize = (float)Settings.Default.brushsize_mm;
            float width = (float)Settings.Default.maxWidthX / brushSize;
            float height = (float)Settings.Default.maxHeightY / brushSize;

            float scale = Math.Min(width / image1.Width, height / image1.Height);
            var scaleWidth = (int)(image1.Width * scale);
            var scaleHeight = (int)(image1.Height * scale);
            Bitmap resized = new Bitmap(image1, new Size(scaleWidth, scaleHeight));

            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(image1, 0, 0, scaleWidth, scaleHeight);
            }

            image1 = resized;
        }

        public static void RefreshPreview()
        {
            String imagePath = Settings.Default.imagePath;
            if (File.Exists(imagePath))
            {
                form1.pictureBox1.Image = image1;
            }
        }

        public static void RotatePicture()
        {
            String imagePath = Settings.Default.imagePath;
            if (File.Exists(imagePath))
            {
                image1.RotateFlip(RotateFlipType.Rotate90FlipNone);
            }
        }

        public static void MirrorPictureX()
        {
            String imagePath = Settings.Default.imagePath;
            if (File.Exists(imagePath))
            {
                image1.RotateFlip(RotateFlipType.RotateNoneFlipX);
            }
        }

        public static void MirrorPictureY()
        {
            String imagePath = Settings.Default.imagePath;
            if (File.Exists(imagePath))
            {
                image1.RotateFlip(RotateFlipType.RotateNoneFlipY);
            }
        }

        public static void DisplayPictureSize()
        {
            float brushSize = (float)Settings.Default.brushsize_mm;
            float xSize, ySize;
            String imagePath = Settings.Default.imagePath;
            if(File.Exists(imagePath))
            {
                xSize = image1.Width * brushSize;
                ySize = image1.Height * brushSize;
                form1.textBox1.Text = "X: " + xSize + "mm; Y: " + ySize + "mm";
                form1.textBox1.Update();
            }
        }

        public static void RecolorImage()
        {
            Cursor.Current = Cursors.WaitCursor;
            GC.Collect();
            for (int x = 0; x < image1.Width; x++)  // Loop through the images pixels
            {
                for (int y = 0; y < image1.Height; y++)
                {
                    //get non indexed image
                    image1 = image1.Clone(new Rectangle(0, 0, image1.Width, image1.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    double colorDistanceOld = 99999999.0f;
                    Color pixelColor = image1.GetPixel(x, y);
                    //source: https://en.wikipedia.org/wiki/Color_difference#Euclidean
                    foreach(ColorDef colorDef in colorPalette)
                    {
                        float rX = (colorDef.Color.R + pixelColor.R) / 2;
                        float deltaR = colorDef.Color.R - pixelColor.R;
                        float deltaG = colorDef.Color.G - pixelColor.G;
                        float deltaB = colorDef.Color.B - pixelColor.B;
                        double colorDistance = Math.Sqrt((2 + rX / 256) * deltaR*deltaR + 4* deltaG*deltaG + (2+ (255-rX)/256) * deltaB*deltaB);
                        if(colorDistance < colorDistanceOld)
                        {
                            colorDistanceOld = colorDistance;
                            image1.SetPixel(x, y, colorDef.Color);
                        }
                        else if(colorDistance == 0)
                        {
                            break;
                        }
                    }
                }
            }
            Cursor.Current = Cursors.Default;
        }

        public static void ParseColors()
        {
            HashSet<ColorDef> colors = new HashSet<ColorDef>();
            String colorDefs = Settings.Default.colorDefinitions;
            var result = colorDefs.Split(new[] { '\r', '\n' });
            foreach(String colorVal in result)
            {
                colors.Add(new ColorDef(colorVal));
            }
            colorPalette = colors;
        }

        static string AztecColorAssign(string hexcolor)
        {
            ParseColors();
            foreach (ColorDef colorDef in colorPalette)
            {
                if (colorDef.ColorHex.Equals(hexcolor))
                {
                    hexcolor = colorDef.Name + "_" + colorDef.ColorHex;
                }
            }
            return hexcolor;
        }
    }
}
