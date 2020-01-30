using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Paintbot
//everything in absolute coordinates (G53)

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
        
        public static void saveSettings()
        {
            //TODO: save and load settings
            //https://stackoverflow.com/questions/3784477/c-sharp-approach-for-saving-user-settings-in-a-wpf-application
            //foreach Settings.Default
            //save as list or dictionary and save/load to xml
            //evaluate the data ->
            //https://stackoverflow.com/questions/7019978/can-i-compare-the-keys-of-two-dictionaries
        }

        public static void loadSettings()
        {

        }

        public static void GroupColors()
        {
            //scale image down and back up to blur it
            float scale = (float)Settings.Default.groupColorFactor;
            int scaleWidth = (int)(image1.Width * scale);
            int scaleHeight = (int)(image1.Height * scale);
            Bitmap resized = new Bitmap(image1, new Size(scaleWidth, scaleHeight));

            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(image1, 0, 0, scaleWidth, scaleHeight);
            }

            image1 = new Bitmap(resized, image1.Width, image1.Height);
            resized.Dispose();
        }

        public static void GenerateGCode(bool generateCircles)
        {
            bool doCircles = generateCircles;

            Cursor.Current = Cursors.WaitCursor;

            //Apply automatic resize and recolor before gcode generation
            if (Settings.Default.resizeColorGcode)
            {
                ResizePicture();
                RecolorImage();
            }

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
            string[,] pathArray = new string[colorStrings.Count, 2];
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

                if (maxNumColorPerFile == 1)
                {
                    fileOut.WriteLine(gcodeStart);
                }

                if (Settings.Default.cleanBrushStart)
                {
                    fileOut.WriteLine(CleanBrush(Settings.Default.useWater, Settings.Default.useSponge, Settings.Default.useTissue, colorContainerHeight, zSpeed, xySpeed, (int)Settings.Default.wiggleAmountTissue, (int)Settings.Default.wiggleAmountWater, (int)Settings.Default.wiggleAmountSponge));
                }

                HashSet<CirclePoint> circlePoints = new HashSet<CirclePoint>();
                HashSet<ColorCoordinate> colorCoordinates = new HashSet<ColorCoordinate>();
                if (doCircles)
                {
                    circlePoints = FillWithCircles((int)Settings.Default.minCircleDiameterPixel, (int)Settings.Default.maxCircleDiameterPixel);
                }
                else
                {
                    colorCoordinates = new HashSet<ColorCoordinate>();
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

                if (doCircles)
                {
                    foreach (CirclePoint circle in circlePoints)
                    {
                        //generate gcode for each complete circle (go through radius)
                        if (circle.Color == colorType)
                        {
                            for (int r = (int)circle.Radius; r > 0; r--)
                            {
                                if (pixelNo % pickColorFrequency == 0)
                                {
                                    if ((getColor + 1) % Settings.Default.cleanBrushPicks == 0)
                                    {
                                        fileOut.WriteLine(CleanBrush(Settings.Default.useWater, false, false, colorContainerHeight, zSpeed, xySpeed, (int)Settings.Default.wiggleAmountTissue, (int)Settings.Default.wiggleAmountWater, (int)Settings.Default.wiggleAmountSponge));
                                    }

                                    fileOut.WriteLine(GetColor(pixelNo, colorPositionX, colorPositionY, colorAtYgantry, colorPositionZ, colorContainerHeight, zSpeed, xySpeed, moveXZsameTime, (float)Settings.Default.colorMoveRadius, false, (int)Settings.Default.wiggleAmountColor, false));
                                    getColor++;
                                }
                                
                                float rad = circle.Radius - r;  //to get exact readius
                                if (flipYAxis)
                                {
                                    fileOut.WriteLine(DrawCircle(circle.CenterX, -circle.CenterY, rad, zMoveHeight, zMoveDepth, brushSize, zSpeed, xySpeed, canvasZeroPosX_mm, canvasZeroPosY_mm, canvasZeroPosZ_mm));
                                }
                                else
                                {
                                    fileOut.WriteLine(DrawCircle(circle.CenterX, circle.CenterY, rad, zMoveHeight, zMoveDepth, brushSize, zSpeed, xySpeed, canvasZeroPosX_mm, canvasZeroPosY_mm, canvasZeroPosZ_mm));
                                }

                                pixelNo++;
                            }
                        }
                    }
                }
                else
                {
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
                            if ((getColor + 1) % Settings.Default.cleanBrushPicks == 0)
                            {
                                fileOut.WriteLine(CleanBrush(Settings.Default.useWater, false, false, colorContainerHeight, zSpeed, xySpeed, (int)Settings.Default.wiggleAmountTissue, (int)Settings.Default.wiggleAmountWater, (int)Settings.Default.wiggleAmountSponge));
                            }
                            fileOut.WriteLine(GetColor(pixelNo, colorPositionX, colorPositionY, colorAtYgantry, colorPositionZ, colorContainerHeight, zSpeed, xySpeed, moveXZsameTime, (float)Settings.Default.colorMoveRadius, false, (int)Settings.Default.wiggleAmountColor, false));
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
                }


                if (endOverWater)
                {   //end with cleaning brush
                    fileOut.WriteLine(CleanBrush(Settings.Default.useWater, Settings.Default.useSponge, Settings.Default.useTissue, colorContainerHeight, zSpeed, xySpeed, (int)Settings.Default.wiggleAmountTissue, (int)Settings.Default.wiggleAmountWater, (int)Settings.Default.wiggleAmountSponge));
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

            //TODO: limit by lines if shorter -> 100 pixel Settings.Default.pixelFileLimit
            //File.ReadLines(@"C:\file.txt").Count(); -> 1000lines
            if (maxNumColorPerFile > 1)
            {
                int noOfFiles = pathArray.Length / 2;
                double numberOfRuns = Math.Ceiling((float)noOfFiles / maxNumColorPerFile);
                for (int k = 0; k < numberOfRuns; k++)
                {
                    int offset = k * maxNumColorPerFile;

                    bool firstRun = true;
                    string fileName = Settings.Default.filePrefix + "_" + k;
                    string colors = "";

                    int upperlimit = offset + maxNumColorPerFile;
                    if (upperlimit > noOfFiles)
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

                    if (maxNumColorPerFile < 11)
                    {
                        //add colors to filename if maxNumColorPerFile not too much
                        fileName = fileName + "_" + colors.Substring(1);
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

            MessageBox.Show("gcode generation done", "PaintCam");
        }

        public static string[,] GetBitmapColorNames(Bitmap bitmap)
        {
            //read whole bmp once in 2-dim array with all colors instead of Bitmap.getPixel
            string[,] colorNames = new string[bitmap.Width, bitmap.Height];

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    colorNames[x, y] = bitmap.GetPixel(x, y).Name;
                }
            }
            return colorNames;
        }

        public static HashSet<CirclePoint> FillWithCircles(int minCircleDiameterPixel, int maxCircleDiameterPixel)
        {
            Cursor.Current = Cursors.WaitCursor;

            if(Settings.Default.filterResizeRecolor)
            {
                ResizePicture();
                RecolorImage();
            }

            if (!Settings.Default.useRectangleErase && minCircleDiameterPixel < 2)
            {
                //catch too small circles for filter with circle erase area
                minCircleDiameterPixel = 2;
                Settings.Default.minCircleDiameterPixel = 2;
            }

            HashSet<string> pictureColors = GetColorStrings(Settings.Default.ignoreColor_hex);
            Bitmap imageCopy = new Bitmap(image1);
            Bitmap circleImage = new Bitmap(image1.Width, image1.Height);
            ColorConverter colorConverter = new ColorConverter();
            using (Graphics gfx = Graphics.FromImage(circleImage))
            using (SolidBrush brush = new SolidBrush((Color)colorConverter.ConvertFromString("#" + Settings.Default.ignoreColor_hex)))
            {
                gfx.FillRectangle(brush, 0, 0, circleImage.Width, circleImage.Height);
            }

            string[,] colorArray = GetBitmapColorNames(imageCopy);

            HashSet<CirclePoint> circlePointSet = new HashSet<CirclePoint>();

            //loop through pixels for colors and when color found loop to left and right check width, then to top and bottom check height, 
            //add both together and calculate center point, make a CirclePoint(int centerX, int centerY, int width, int height)
            foreach (string color in pictureColors)
            {
                bool colorDone = false;
                while (!colorDone)
                {   //repeat until color not found anymore -> next color
                    CirclePoint circlePoint = new CirclePoint(0, 0, 0, 0, "");
                    CirclePoint temp = new CirclePoint(0, 0, 0, 0, "");
                    for (int y = 0; y < imageCopy.Height; y++)
                    {
                        for (int x = 0; x < imageCopy.Width; x++)
                        {
                            if(colorArray[x,y].Equals(color))
                            {
                                int xLeft = 0, xRight = 0, yTop = 0, yBottom = 0;
                                float xWidth = 0, yHeight = 0;
                                if(x > 0)
                                {
                                    for(xLeft = x; xLeft > 0; xLeft--)
                                    {
                                        if(!colorArray[xLeft, y].Equals(color))
                                        {
                                            xLeft = xLeft + 1;
                                            break;
                                        }
                                    }
                                }

                                for(xRight = x; xRight < imageCopy.Width; xRight++)
                                {
                                    if (!colorArray[xRight, y].Equals(color))
                                    {
                                        xRight = xRight - 1;
                                        break;
                                    }
                                }
                                if (xRight == imageCopy.Width)
                                {
                                    //catch when color reaches picture border
                                    xRight = xRight - 1;
                                }

                                xWidth = xRight - xLeft + 1;
                                if(xWidth > maxCircleDiameterPixel)
                                {
                                    xWidth = maxCircleDiameterPixel;
                                }
                                
                                if (y > 0)
                                {
                                    for (yTop = y; yTop > 0; yTop--)
                                    {
                                        if (!colorArray[x, yTop].Equals(color))
                                        {
                                            yTop = yTop + 1;
                                            break;
                                        }
                                    }
                                }

                                for (yBottom = y; yBottom < imageCopy.Height; yBottom++)
                                {
                                    if (!colorArray[x, yBottom].Equals(color))
                                    {
                                        yBottom = yBottom - 1;
                                        break;
                                    }
                                }
                                if(yBottom == imageCopy.Height)
                                {
                                    //catch when color reaches picture border
                                    yBottom = yBottom - 1;
                                }

                                yHeight = yBottom - yTop + 1;
                                if (yHeight > maxCircleDiameterPixel)
                                {
                                    yHeight = maxCircleDiameterPixel;
                                }
                                
                                //move x and y to center
                                float xCenter = (xLeft + xRight) / 2.0f;
                                float yCenter = (yBottom + yTop) / 2.0f;

                                //ensure to keep circle within range
                                if ((xRight - xLeft) > maxCircleDiameterPixel)
                                {
                                    xCenter = xLeft + maxCircleDiameterPixel / 2.0f;
                                }
                                if ((yBottom - yTop) > maxCircleDiameterPixel)
                                {
                                    yCenter = yTop + maxCircleDiameterPixel / 2.0f;
                                }

                                //to catch areas which could hold multiple circles
                                if ((yBottom - yTop) >= 2 * (xRight - xLeft))
                                {
                                    yCenter = yBottom - (xRight - xLeft) / 2.0f;
                                }
                                else if ((xRight - xLeft) >= 2 * (yBottom - yTop))
                                {
                                    xCenter = xRight - (yBottom - yTop) / 2.0f;
                                }

                                if (!colorArray[(int)xCenter, (int)yCenter].Equals(color))
                                {
                                    //don´t accept centerpoints which have a different color
                                    xWidth = 0;
                                    yHeight = 0;
                                }
                                
                                temp = new CirclePoint(xCenter, yCenter, xWidth, yHeight, color);
                                
                                if (temp.Diameter > circlePoint.Diameter)
                                {
                                    //if next point has bigger Width+Height replace Circlepoint after looping through image draw a circle with smaller value of height and width around the centerpoint in circleImage. 
                                    circlePoint = temp;
                                }
                                if(temp.Diameter < minCircleDiameterPixel)
                                {
                                    //erase areas which are too small
                                    colorArray[(int)xCenter, (int)yCenter] = Settings.Default.ignoreColor_hex;
                                }
                            }
                        }
                        
                    }
                    if (circlePoint.Radius == 0)
                    {
                        colorDone = true;
                    }
                    else
                    {
                        //overwrite that area with ignorecolor circle in imageCopy. If after looping circle would be < then minCircleDiameterPixel overwrite with ignorcolor without drawing circle.
                        //if circle after loop is > maxCircleDiameterPixel, draw circle with maxCircleDiameterPixel at centerpoint
                        if(circlePoint.Diameter > maxCircleDiameterPixel)
                        {
                            circlePoint.Radius = maxCircleDiameterPixel / 2;
                            circlePoint.Width = maxCircleDiameterPixel;
                            circlePoint.Height = maxCircleDiameterPixel;
                            circlePoint.Diameter = maxCircleDiameterPixel;
                        }
                        
                        if (Settings.Default.useRectangleErase)
                        {
                            //erase rectangle
                            for (int y = (int)circlePoint.GetCircleYmin(); y < circlePoint.GetCircleYmax() + 1; y++)
                            {
                                for (int x = (int)circlePoint.GetCircleXmin(); x < circlePoint.GetCircleXmax() + 1; x++)
                                {
                                    if(x < colorArray.GetLength(0) && y < colorArray.GetLength(1))
                                    {
                                        colorArray[x, y] = Settings.Default.ignoreColor_hex;
                                    }
                                }
                            }
                        }
                        else
                        {
                            //erase circle instead of rectangle
                            for (int r = 0; r < (int)Math.Round(circlePoint.Radius); r++)
                            {
                                for (int phi = 0; phi < 360; phi++)
                                {
                                    int x = (int)(circlePoint.CenterX + r * Math.Cos(phi * Math.PI / 180));
                                    int y = (int)(circlePoint.CenterY + r * Math.Sin(phi * Math.PI / 180));
                                    colorArray[x, y] = Settings.Default.ignoreColor_hex;
                                }
                            }
                        }

                        //draw the preview with filled circles
                        Rectangle rect = new Rectangle((int)circlePoint.GetCircleXmin(), (int)circlePoint.GetCircleYmin(), (int)circlePoint.Width, (int)circlePoint.Height);
                        if (circlePoint.Radius < 1.0 && circlePoint.Radius != 0)
                        {
                            //catch 1 pixel circles
                            rect = new Rectangle((int)circlePoint.CenterX, (int)circlePoint.CenterY, 1, 1);
                        }

                        Graphics gTemp = Graphics.FromImage(imageCopy);
                        Graphics gCircle = Graphics.FromImage(circleImage);
                        SolidBrush brushTemp = new SolidBrush((Color)colorConverter.ConvertFromString("#" + Settings.Default.ignoreColor_hex));

                        gTemp.FillEllipse(brushTemp, rect);
                        SolidBrush brushColor = new SolidBrush((Color)colorConverter.ConvertFromString("#" + circlePoint.Color));
                        gTemp.Dispose();

                        if (circlePoint.Diameter >= minCircleDiameterPixel)
                        {
                            gCircle.FillEllipse(brushColor, rect);
                            gCircle.Dispose();
                        }

                        brushTemp.Dispose();
                        brushColor.Dispose();
                        //end of preview

                        circlePointSet.Add(circlePoint);
                        temp = new CirclePoint(0,0,0,0, "");
                        Settings.Default.amountOfCircles = circlePointSet.Count.ToString();
                        form1.textBox3.Refresh();
                    }
                    form1.pictureBox2.Image = imageCopy;
                    form1.pictureBox2.Refresh();
                }
                
                form1.pictureBox2.Image = imageCopy;
                form1.pictureBox2.Refresh();
            }

            image1 = new Bitmap(circleImage);
            RefreshPreview();
            imageCopy.Dispose();
            circleImage.Dispose();

            Settings.Default.amountOfCircles = circlePointSet.Count.ToString();
            Cursor.Current = Cursors.Default;

            return circlePointSet;
        }

        public static void DrawHollowCircles()
        {
            HashSet<CirclePoint> circlePoints = FillWithCircles((int)Settings.Default.minCircleDiameterPixel, (int)Settings.Default.maxCircleDiameterPixel);

            Bitmap emptyBmp = new Bitmap(image1.Width, image1.Height);
            Graphics graphics = Graphics.FromImage(emptyBmp);
            ColorConverter colorConverter = new ColorConverter();

            foreach(CirclePoint circlePoint in circlePoints)
            {
                Rectangle rect = new Rectangle((int)circlePoint.GetCircleXmin(), (int)circlePoint.GetCircleYmin(), (int)circlePoint.Width, (int)circlePoint.Height);
                Pen pen = new Pen((Color)colorConverter.ConvertFromString("#" + circlePoint.Color));

                graphics.DrawEllipse(pen, rect);
            }

            image1 = new Bitmap(emptyBmp);
            RefreshPreview();

            emptyBmp.Dispose();
            graphics.Dispose();
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

        static string GetColor(int getColorIndex, float xPos, float yPos, bool colorAtYgantry, float zPos, float zColorHeight, int zSpeed, int xySpeed, bool moveXZsameTime, float xyMoveRadius, bool brushClean, int wiggleAmount, bool moveRandom)
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

            string wiggle = "";
            Random rand = new Random();

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
                    for(int i = 0; i < wiggleAmount; i++)
                    {
                        if (moveRandom)
                        {
                            //Random on tissue and sponge
                            xPos = xPos + xyMoveRadius * (float)rand.NextDouble() * 0.1f;
                            yPos = yPos + xyMoveRadius * (float)rand.NextDouble() * 0.3f;
                        }
                        wiggle = wiggle + "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed + "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed;
                    }
                    getColorString = "\nG53 X" + xPos.ToString().Replace(',', '.') + " Z" + (zColorHeight + zPos) + " F" + (int)feedRate + gcodePartStart +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    wiggle +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + gcodePartEnd;
                }
                else
                {
                    for (int i = 0; i < wiggleAmount; i++)
                    {
                        if (moveRandom)
                        {
                            //Random on tissue and sponge
                            xPos = xPos + xyMoveRadius * (float)rand.NextDouble() * 0.1f;
                            yPos = yPos + xyMoveRadius * (float)rand.NextDouble() * 0.3f;
                        }
                        wiggle = wiggle + "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                        "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed;
                    }
                    getColorString = "\nG53 X" + xPos.ToString().Replace(',', '.') + " Y" + yPos.ToString().Replace(',', '.') + " Z" + (zColorHeight + zPos) + " F" + (int)feedRate + gcodePartStart +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    wiggle +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " Y" + yPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + gcodePartEnd;
                }

            }
            else
            {
                if (colorAtYgantry)
                {
                    for (int i = 0; i < wiggleAmount; i++)
                    {
                        if (moveRandom)
                        {
                            //Random on tissue and sponge
                            xPos = xPos + xyMoveRadius * (float)rand.NextDouble() * 0.1f;
                            yPos = yPos + xyMoveRadius * (float)rand.NextDouble() * 0.3f;
                        }
                        wiggle = wiggle + "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed + "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed;
                    }
                    getColorString = "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + gcodePartStart +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + //" Y" + yPos.ToString().Replace(',', '.') + 
                    " F" + xySpeed +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    wiggle +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + gcodePartEnd;
                }
                else
                {
                    for (int i = 0; i < wiggleAmount; i++)
                    {
                        if (moveRandom)
                        {
                            //Random on tissue and sponge
                            xPos = xPos + xyMoveRadius * (float)rand.NextDouble() * 0.1f;
                            yPos = yPos + xyMoveRadius * (float)rand.NextDouble() * 0.3f;
                        }
                        wiggle = wiggle + "\nG53 X" + (xPos + xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos + xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed +
                        "\nG53 X" + (xPos - xyMoveRadius).ToString().Replace(',', '.') + " Y" + (yPos - xyMoveRadius).ToString().Replace(',', '.') + " F" + xySpeed;
                    }
                    getColorString = "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + gcodePartStart +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " Y" + yPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    wiggle +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " Y" + yPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + gcodePartEnd;
                }
            }

            oldXpos = xPos;
            oldYpos = yPos;
            oldZpos = zPos;

            return getColorString;
        }

        static string CleanBrush(bool useWater, bool useSponge, bool useTissue, float zColorHeight, int zSpeed, int xySpeed, int amountWiggleTissue, int amountWiggleWater, int amountWiggleSponge) 
        {
            //automatic brush cleaning
            string cleanBrush = "";
            //water
            if (useWater)
            {
                cleanBrush = cleanBrush + GetColor(1, (float)Settings.Default.waterPosX_mm, (float)Settings.Default.waterPosY_mm, false, (float)Settings.Default.waterPosZ_mm, (float)Settings.Default.waterContainerHeight_mm, zSpeed, xySpeed, false, (float)Settings.Default.waterMoveRadius, true, (int)Settings.Default.wiggleAmountWater, false);
            }
            //tissue
            if (useTissue)
            {
                cleanBrush = cleanBrush + GetColor(1, (float)Settings.Default.tissuePosX_mm, (float)Settings.Default.tissuePosY_mm, false, (float)Settings.Default.tissuePosZ_mm, zColorHeight, zSpeed, xySpeed, false, (float)Settings.Default.tissueMoveRadius, true, (int)Settings.Default.wiggleAmountTissue, true);
            }
            //sponge
            if (useSponge)
            {
                cleanBrush = cleanBrush + GetColor(1, (float)Settings.Default.spongePosX_mm, (float)Settings.Default.spongePosY_mm, false, (float)Settings.Default.spongePosZ_mm, zColorHeight, zSpeed, xySpeed, false, (float)Settings.Default.spongeMoveRadius, true, (int)Settings.Default.wiggleAmountSponge, true);
            }
            //water
            if (useWater)
            {
                cleanBrush = cleanBrush + GetColor(1, (float)Settings.Default.waterPosX_mm, (float)Settings.Default.waterPosY_mm, false, (float)Settings.Default.waterPosZ_mm, (float)Settings.Default.waterContainerHeight_mm, zSpeed, xySpeed, false, (float)Settings.Default.waterMoveRadius, true, (int)Settings.Default.wiggleAmountWater, false);
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
                if (image1.Width < (int)width && image1.Height >= (int)height - 1)
                {
                    int xOffset = ((int)width - image1.Width) / 2;
                    canvasZeroPosX_mm = canvasZeroPosX_mm + xOffset;
                }
                else if (image1.Width >= (int)width - 1 && image1.Height < (int)height)
                {
                    int yOffset = ((int)height - image1.Height) / 2;
                    if (Settings.Default.flipYAxis)
                    {
                        canvasZeroPosY_mm = canvasZeroPosY_mm - yOffset;
                    }
                    else
                    {
                        canvasZeroPosY_mm = canvasZeroPosY_mm + yOffset;
                    }
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

        static string DrawCircle(float xCenter, float yCenter, float radius, float zMoveHeight, float zMoveDepth, float brushSize, int zSpeed, int xySpeed, float canvasZeroPosX_mm, float canvasZeroPosY_mm, float canvasZeroPosZ_mm)
        {
            //generate circle drawings gcode
            //http://www.manufacturinget.org/2011/12/cnc-g-code-g02-and-g03/
            
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
            //check circle gcode example:
            //G17; set plane
            //G53 X120 Y - 120 F3000; move to left edge of the circle
            //G10 P0 L20 X0 Y0 Z0; reset relative X and Y
            //G01 X0 Y0 F3000; start relative move
            //G03 X0 Y0 I10; start circle
            //G01 X0 Y0 F3000; end relative move
            string circleString = "\nG17;" +
                "\nG53 X" + ((xCenter - radius) * brushSize + canvasZeroPosX_mm).ToString().Replace(',', '.') + " Y" + (yCenter * brushSize + canvasZeroPosY_mm).ToString().Replace(',', '.') + " F" + xySpeed +
                "\nG53 Z" + (zMoveDepth + canvasZeroPosZ_mm).ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                "\nG10 P0 L20 X0 Y0 \nG01 X0 Y0 F" + xySpeed +
                "\nG03 X0 Y0 I" + radius.ToString().Replace(',', '.') + "; draw circle " +
                "\nG01 X0 Y0 F" + xySpeed +
                "\nG53 Z" + (zMoveHeight + canvasZeroPosZ_mm).ToString().Replace(',', '.') + " F" + zSpeed + "; circle end";

            oldXpos = (xCenter - radius) * brushSize + canvasZeroPosX_mm;
            oldYpos = yCenter * brushSize + canvasZeroPosY_mm;
            oldZpos = zMoveHeight + canvasZeroPosZ_mm;

            return circleString;
        }

        public static void LoadImage()
        {
            String imagePath = Settings.Default.imagePath;
            if (File.Exists(imagePath))
            {
                image1 = new Bitmap(imagePath);
            }
        }

        public static void ResizePicture()
        {
            float brushSize = (float)Settings.Default.brushsize_mm;
            float width = (float)Settings.Default.maxWidthX / brushSize;
            float height = (float)Settings.Default.maxHeightY / brushSize;

            float scale = Math.Min(width / image1.Width, height / image1.Height);
            int scaleWidth = (int)(image1.Width * scale);
            int scaleHeight = (int)(image1.Height * scale);
            Bitmap resized = new Bitmap(image1, new Size(scaleWidth, scaleHeight));

            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(image1, 0, 0, scaleWidth, scaleHeight);
            }

            image1 = new Bitmap(resized);
            resized.Dispose();
            DisplayPictureSize();
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
                if(image1 == null)
                {
                    image1 = new Bitmap(imagePath);
                }
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

            //get non indexed image
            image1 = image1.Clone(new Rectangle(0, 0, image1.Width, image1.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            for (int x = 0; x < image1.Width; x++)  // Loop through the images pixels
            {
                for (int y = 0; y < image1.Height; y++)
                {
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
                        if(colorDistance == 0)
                        {
                            break;
                        }
                        else if (colorDistance < colorDistanceOld)
                        {
                            colorDistanceOld = colorDistance;
                            image1.SetPixel(x, y, colorDef.Color);
                        }
                    }
                }
            }
            //display amount of colors
            Settings.Default.amountOfColors = GetColorStrings(Settings.Default.ignoreColor_hex).Count.ToString();

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
