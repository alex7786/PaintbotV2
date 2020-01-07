using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Paintbot
    //everything in absolute coordinates (G53)
    //TODO:
    //Center picture on canvas
    //Find nearest color
    //use colorpalette
    //resize picture without blurring it
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
            bool endOverPaint = Settings.Default.endOverPaint;
            bool moveXZsameTime = Settings.Default.moveZXsameTime;
            float canvasZeroPosX_mm = (float)Settings.Default.canvasZeroPosX_mm;
            float canvasZeroPosY_mm = (float)Settings.Default.canvasZeroPosY_mm;
            float canvasZeroPosZ_mm = (float)Settings.Default.canvasZeroPosZ_mm;
            int progressBar = Settings.Default.progressbar; //value between 0 and 100

            Directory.CreateDirectory(outputPath);
            DirectoryInfo di = new DirectoryInfo(outputPath);

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }

            string gcodeStart = System.IO.File.ReadAllText(gcodeStartPath);
            string gcodeEnd = System.IO.File.ReadAllText(gcodeEndPath);

            int x, y;

            HashSet<string> colorStrings = new HashSet<string>();

            for (x = 0; x < image1.Width; x++)  // Loop through the images pixels
            {
                for (y = 0; y < image1.Height; y++)
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

            int progress = 0;

            foreach (string colorType in colorStrings)
            {
                float progressRatio = (float)progress / (float)colorStrings.Count;
                progressBar = (int)(progressRatio * 100);
                progress++;
                form1.progressBar1.Value = progressBar;
                form1.progressBar1.Update();

                StreamWriter fileOut = new StreamWriter(Path.Combine(outputPath, AztecColorAssign(colorType) + ".gcode"), true);

                fileOut.WriteLine(gcodeStart);

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

                    if (getColor % pickColorFrequency == 0)
                    {
                        fileOut.WriteLine(GetColor(getColor, colorPositionX, colorPositionY, colorAtYgantry, colorPositionZ, colorContainerHeight, zSpeed, xySpeed, moveXZsameTime));
                    }
                    getColor++;
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

                if (endOverPaint)
                {
                    fileOut.WriteLine(GetColor(getColor, colorPositionX, colorPositionY, colorAtYgantry, colorPositionZ, colorContainerHeight, zSpeed, xySpeed, moveXZsameTime));
                    //end with getting color
                }
                else
                {
                    fileOut.WriteLine(gcodeEnd);
                }

                fileOut.Close();
            }

            form1.progressBar1.Value = 100;
            form1.progressBar1.Update();
            var formPopup = new Form2(form1);
            formPopup.Show();
        }

        static string GetColor(int getColorIndex, float xPos, float yPos, bool colorAtYgantry, float zPos, float zColorHeight, int zSpeed, int xySpeed, bool moveXZsameTime)
        {
            /*
             * xPos = X position of the color
             * yPos = Y position of the color
             * zPos = Z position of the color
             * zColorHeight = height of the color container plus an offset to get over the edge
            */

            string getColorString = "";

            if (moveXZsameTime) //TODO: consider absolute and relative coordinates
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
                    getColorString = "\nG53 X" + xPos.ToString().Replace(',', '.') + " Z" + (zColorHeight + zPos) + " F" + (int)feedRate + "; get paint start " +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    "\nG53 X" + (xPos + 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos + 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + "; get paint end ";
                }
                else
                {
                    getColorString = "\nG53 X" + xPos.ToString().Replace(',', '.') + " Y" + yPos.ToString().Replace(',', '.') + " Z" + zColorHeight + " F" + (int)feedRate + "; get paint start " +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    "\nG53 X" + (xPos + 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos + 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + zColorHeight + " F" + zSpeed + "; get paint end ";
                }

            }
            else
            {
                if (colorAtYgantry)
                {
                    getColorString = "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + "; get paint start " +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + //" Y" + yPos.ToString().Replace(',', '.') + 
                    " F" + xySpeed +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    "\nG53 X" + (xPos + 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos + 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + (zColorHeight + zPos) + " F" + zSpeed + "; get paint end ";
                }
                else
                {
                    getColorString = "\nG53 Z" + zColorHeight + " F" + zSpeed + "; get paint start " +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " Y" + yPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + zPos.ToString().Replace(',', '.') + " F" + zSpeed + "; lower Z " +
                    "\nG53 X" + (xPos + 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos + 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + (xPos - 3).ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 X" + xPos.ToString().Replace(',', '.') + " F" + xySpeed +
                    "\nG53 Z" + zColorHeight + " F" + zSpeed + "; get paint end ";
                }
            }

            oldXpos = xPos;
            oldYpos = yPos;
            oldZpos = zPos;

            return getColorString;
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

        /*Color definitions
        a101/ffffffff/10.0/10.0
        a139/ffecda3e/10.1/10.1
        a102/fff4ed56/10.2/10.2
        a121/ffeecf3d/10.3/10.3
        a140/ffe39d33/10.4/10.4
        a134/ff8369bf/10.5/10.5
        a125/ff3c3333/10.6/10.6
        a111/ff433866/10.7/10.7
        a110/ff4b4273/10.8/10.8
        a116/ff3c334d/10.9/10.9
        a113/ffcf9a38/10.10/10.10
        a127/ffb48347/10.11/10.11
        a144/ffa97537/10.12/10.12
        a131/fff1d69e/10.13/10.13
        a148/ffde7b2d/10.14/10.14
        a152/ffb4553d/10.15/10.15
        a114/ffdc5b2c/10.16/10.16
        a126/ffd94834/10.17/10.17
        a129/ffdoa574/10.18/10.18
        a106/ffd73d35/10.19/10.19
        a145/ffda4538/10.20/10.20
        a142/ffd83f38/10.21/10.21
        a161/ffd93c44/10.22/10.22
        a155/ffce383c/10.23/10.23
        a105/ffd13d43/10.24/10.24
        a150/ff9a3138/10.25/10.25
        a138/ffd1333c/10.26/10.26
        a115/ffd51e81/10.27/10.27
        a146/ffdc37be/10.28/10.28
        a157/ffdb4f76/10.29/10.29
        a141/ffe59cd0/10.30/10.30
        a135/fff4e2b7/10.31/10.31
        a124/ff7197f7/10.32/10.32
        a147/ff5d84dd/10.33/10.33
        a123/ff5d8ee1/10.34/10.34
        a154/ff69a1f5/10.35/10.35
        a120/ff72c898/10.36/10.36
        a103/ff262c3a/10.37/10.37
        a136/ff40706e/10.38/10.38
        a122/ff294321/10.39/10.39
        a158/ff282d39/10.40/10.40
        a143/ff325719/10.41/10.41
        a149/ff41684d/10.42/10.42
        a159/ff4c8a54/10.43/10.43
        a109/ff6ec856/10.44/10.44
        a156/ff52911f/10.45/10.45
        a137/ff5f643c/10.46/10.46
        a119/ffbb843a/10.47/10.47
        a118/ff42372f/10.48/10.48
        a130/ff6b5340/10.49/10.49
        a108/ff5b3a31/10.50/10.50
        a151/ff833130/10.51/10.51
        a107/ff9a4635/10.52/10.52
        a153/ff5a392f/10.53/10.53
        a160/ff2a292e/10.54/10.54
        a162/ff38302d/10.55/10.55
        a112/ff000000/10.56/10.56
        a132/ffb0b1b5/10.57/10.57
        a117/ffb3c8e5/10.58/10.58
        a128/ffcacace/10.59/10.59
        */

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
            switch (hexcolor)
            {
                case "ffffffff":
                    hexcolor += "_101";
                    break;
                case "ffecda3e":
                    hexcolor += "_139";
                    break;
                case "fff4ed56":
                    hexcolor += "_102";
                    break;
                case "ffeecf3d":
                    hexcolor += "_121";
                    break;
                case "ffe39d33":
                    hexcolor += "_140";
                    break;
                case "ff8369bf":
                    hexcolor += "_134";
                    break;
                case "ff3c3333":
                    hexcolor += "_125";
                    break;
                case "ff433866":
                    hexcolor += "_111";
                    break;
                case "ff4b4273":
                    hexcolor += "_110";
                    break;
                case "ff3c334d":
                    hexcolor += "_116";
                    break;
                case "ffcf9a38":
                    hexcolor += "_113";
                    break;
                case "ffb48347":
                    hexcolor += "_127";
                    break;
                case "ffa97537":
                    hexcolor += "_144";
                    break;
                case "fff1d69e":
                    hexcolor += "_131";
                    break;
                case "ffde7b2d":
                    hexcolor += "_148";
                    break;
                case "ffb4553d":
                    hexcolor += "_152";
                    break;
                case "ffdc5b2c":
                    hexcolor += "_114";
                    break;
                case "ffd94834":
                    hexcolor += "_126";
                    break;
                case "ffdoa574":
                    hexcolor += "_129";
                    break;
                case "ffd73d35":
                    hexcolor += "_106";
                    break;
                case "ffda4538":
                    hexcolor += "_145";
                    break;
                case "ffd83f38":
                    hexcolor += "_142";
                    break;
                case "ffd93c44":
                    hexcolor += "_161";
                    break;
                case "ffce383c":
                    hexcolor += "_155";
                    break;
                case "ffd13d43":
                    hexcolor += "_105";
                    break;
                case "ff9a3138":
                    hexcolor += "_150";
                    break;
                case "ffd1333c":
                    hexcolor += "_138";
                    break;
                case "ffd51e81":
                    hexcolor += "_115";
                    break;
                case "ffdc37be":
                    hexcolor += "_146";
                    break;
                case "ffdb4f76":
                    hexcolor += "_157";
                    break;
                case "ffe59cd0":
                    hexcolor += "_141";
                    break;
                case "fff4e2b7":
                    hexcolor += "_135";
                    break;
                case "ff7197f7":
                    hexcolor += "_124";
                    break;
                case "ff5d84dd":
                    hexcolor += "_147";
                    break;
                case "ff5d8ee1":
                    hexcolor += "_123";
                    break;
                case "ff69a1f5":
                    hexcolor += "_154";
                    break;
                case "ff72c898":
                    hexcolor += "_120";
                    break;
                case "ff262c3a":
                    hexcolor += "_103";
                    break;
                case "ff40706e":
                    hexcolor += "_136";
                    break;
                case "ff294321":
                    hexcolor += "_122";
                    break;
                case "ff282d39":
                    hexcolor += "_158";
                    break;
                case "ff325719":
                    hexcolor += "_143";
                    break;
                case "ff41684d":
                    hexcolor += "_149";
                    break;
                case "ff4c8a54":
                    hexcolor += "_159";
                    break;
                case "ff6ec856":
                    hexcolor += "_109";
                    break;
                case "ff52911f":
                    hexcolor += "_156";
                    break;
                case "ff5f643c":
                    hexcolor += "_137";
                    break;
                case "ffbb843a":
                    hexcolor += "_119";
                    break;
                case "ff42372f":
                    hexcolor += "_118";
                    break;
                case "ff6b5340":
                    hexcolor += "_130";
                    break;
                case "ff5b3a31":
                    hexcolor += "_108";
                    break;
                case "ff833130":
                    hexcolor += "_151";
                    break;
                case "ff9a4635":
                    hexcolor += "_107";
                    break;
                case "ff5a392f":
                    hexcolor += "_153";
                    break;
                case "ff2a292e":
                    hexcolor += "_160";
                    break;
                case "ff38302d":
                    hexcolor += "_162";
                    break;
                case "ff000000":
                    hexcolor += "_112";
                    break;
                case "ffb0b1b5":
                    hexcolor += "_132";
                    break;
                case "ffb3c8e5":
                    hexcolor += "_117";
                    break;
                case "ffcacace	":
                    hexcolor += "_128";
                    break;
                default:
                    break;
            }
            return hexcolor;
        }
    }
}
