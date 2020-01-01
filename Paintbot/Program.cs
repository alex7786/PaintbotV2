using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
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
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            form1 = new Form1();
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

            Bitmap image1 = new Bitmap(imagePath);

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
                    Color pixelColor = image1.GetPixel(x, y);
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

                HashSet<colorCoordinate> colorCoordinates = new HashSet<colorCoordinate>();
                for (x = 0; x < image1.Width; x++)  // Loop through the images pixels
                {
                    for (y = 0; y < image1.Height; y++)
                    {
                        Color pixelColor = image1.GetPixel(x, y);
                        {
                            if (string.Equals(pixelColor.Name, colorType))
                            {
                                colorCoordinates.Add(new colorCoordinate(x, y));
                            }
                        }
                    }
                }

                int getColor = 0;
                colorCoordinate currentPoint = null;
                colorCoordinate nextPoint = null;
                HashSet<colorCoordinate> doneColorCoordinates = new HashSet<colorCoordinate>();

                foreach (colorCoordinate point in colorCoordinates)
                {
                    if (currentPoint == null)
                    {   //initialization of the first point
                        currentPoint = point;
                        nextPoint = point;
                        doneColorCoordinates.Add(point);
                    }
                    
                    
                    foreach (colorCoordinate nPoint in colorCoordinates)
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
