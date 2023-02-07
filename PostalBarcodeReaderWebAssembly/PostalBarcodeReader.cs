using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using OpenCvSharp;
using SixLabors.Fonts.Unicode;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PostalBarcodeReaderWebAssembly
{
    class PostalBarcodeReader
    {
        // StartCode(1 * 2 Line) + Postcode(7*3) + Address(13 * 3) + CheckDigit(1 * 3) + StopCode(1 * 2)
        const int LINES = 67;
        public static int g_RowInterval = 10; //10
        public static double g_maxOrientation = 3.1; //5.1
        public static double g_intervalOrientation = 0.2; // 1.0
        public static double g_toleranceRateOfBarlength = 0.75; //最初と最後のバーの長さの許容率
        public static double g_toleranceRateOfReadPoint = 0.65; //読み取り点の位置の許容率
        public struct StructLine
        {
            public int x1;
            public int y1;
            public int x2;
            public int y2;
        }

        public struct StructHit
        {
            public StructLine line;
            public OpenCvSharp.Rect rect;
            public string barcodeNum;
            public string decoded;
            public string Label;
            public bool isUpsiteDown;
        }
        public struct StructPixel
        {
            public OpenCvSharp.Point pt;
            public double distance;
            public double angle;
        }

        // バーの数値化ルール ■→黒、□→白
        // ■
        // ■ → 1
        // ■
        //
        // ■
        // ■ → 2
        // □
        //
        // □
        // ■ → 3
        // ■
        //
        // □
        // ■ → 4
        // □
        private static ReadOnlyDictionary<int, string> DIC_CODE = new ReadOnlyDictionary<int, string>(new Dictionary<int, string>()
    {
        {114, "1" },
        {132, "2" },
        {312, "3" },
        {123, "4" },
        {141, "5" },
        {321, "6" },
        {213, "7" },
        {231, "8" },
        {411, "9" },
        {144, "0" },
        {414, "-" },
        {324, "CC1" },
        {342, "CC2" },
        {234, "CC3" },
        {432, "CC4" },
        {243, "CC5" },
        {423, "CC6" },
        {441, "CC7" },
        {111, "CC8" },
        {13, "Start" },
        {31, "Stop" },
        {324144, "A" },
        {324114, "B" },
        {324132, "C" },
        {324312, "D" },
        {324123, "E" },
        {324141, "F" },
        {324321, "G" },
        {324213, "H" },
        {324231, "I" },
        {324411, "J" },
        {342144, "K" },
        {342114, "L" },
        {342132, "M" },
        {342312, "N" },
        {342123, "O" },
        {342141, "P" },
        {342321, "Q" },
        {342213, "R" },
        {342231, "S" },
        {342411, "T" },
        {234144, "U" },
        {234114, "V" },
        {234132, "W" },
        {234312, "X" },
        {234123, "Y" },
        {234141, "Z" },
    });
        private static ReadOnlyDictionary<int, int> DIC_CODE_CD = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>()
    {
        {114, 1 },
        {132, 2 },
        {312, 3 },
        {123, 4 },
        {141, 5 },
        {321, 6 },
        {213, 7 },
        {231, 8 },
        {411, 9 },
        {144, 0 },
        {414, 10 },
        {324, 11 },
        {342, 12 },
        {234, 13 },
        {432, 14 },
        {243, 15 },
        {423, 16 },
        {441, 17 },
        {111, 18 },
    });

        public static void DetectBarcodeAll(
            in OpenCvSharp.Mat src
            , out List<StructHit> rtnListSHit
            , bool singleDetectMode = false
            , int prmRowInterval = 10
            , double prmMaxOrientation = 3.1
            , double prmIntervalOrientation = 0.2
            , double prmToleranceRateOfBarlength = 0.75
            , double prmToleranceRateOfReadPoint = 0.65
            )
        {
            // パラメータの設定
            g_RowInterval = prmRowInterval;
            g_maxOrientation = prmMaxOrientation;
            g_intervalOrientation = prmIntervalOrientation;
            g_toleranceRateOfBarlength = prmToleranceRateOfBarlength;
            g_toleranceRateOfReadPoint = prmToleranceRateOfReadPoint;

            // 検出結果の格納先
            var listSHit = new List<StructHit>();

            Size size = src.Size();
            Point ptCenter = new Point(src.Cols / 2, src.Rows / 2);
            Mat srcCopy = src.Clone();
            Mat<byte> src8U = new Mat<byte>();

            // 画像を加工し、検出精度を上げる
            Gamma(srcCopy.Clone(), out srcCopy, 2.0);
            Cv2.CvtColor(srcCopy, srcCopy, ColorConversionCodes.BGR2GRAY);
            UnsharpMasking(srcCopy.Clone(), out srcCopy, 5.0f);
            Cv2.AdaptiveThreshold(srcCopy, srcCopy, 255, OpenCvSharp.AdaptiveThresholdTypes.GaussianC, OpenCvSharp.ThresholdTypes.Binary, 51, 20);
            opening(srcCopy, out srcCopy, 0.2f);
            srcCopy.ConvertTo(src8U, MatType.CV_8U);

            DetectBarcode(src8U, ref listSHit, "default", singleDetectMode);
            if (singleDetectMode && listSHit.Count > 0)
            {
                rtnListSHit = listSHit;
            }
            else
            {
                var listAngle = new List<double>();
                double angle = g_intervalOrientation;
                while (angle < g_maxOrientation)
                {
                    listAngle.Add(angle);
                    angle += g_intervalOrientation;
                }
                angle = -g_intervalOrientation;
                while (angle > -g_maxOrientation)
                {
                    listAngle.Add(angle);
                    angle -= g_intervalOrientation;
                }

                var listAngle2 = listAngle
                                .OrderBy(x => Math.Abs(x));

                foreach (var dAngle in listAngle2)
                {
                    double angle2 = dAngle;
                    string wLabel = angle2.ToString();

                    var trans = Cv2.GetRotationMatrix2D(ptCenter, angle2, 1.0);
                    using (var src8U_2 = src8U.Clone())
                    {
                        Cv2.WarpAffine(src8U, src8U_2, trans, size);
                        List<StructHit> listSHit2 = rotateHitList(listSHit, angle2 * -1, ptCenter);
                        DetectBarcode(src8U_2, ref listSHit2, wLabel, singleDetectMode);
                        var resultSHit2 = listSHit2.FindAll(x => x.Label == wLabel);
                        resultSHit2 = rotateHitList(resultSHit2, angle2, ptCenter);
                        if (resultSHit2.Count > 0)
                        {
                            listSHit.AddRange(resultSHit2);
                            if (singleDetectMode)
                            {
                                break;
                            }

                        }
                    }
                };
                GC.Collect();

                rtnListSHit = listSHit;
            }

        }

        public static void DetectBarcodeAll(
            in OpenCvSharp.Mat src
            , out List<StructHit> rtnListSHit
            , OpenCvSharp.Rect targetArea
            , bool singleDetectMode = false
            , int prmRowInterval = 10
            , double prmMaxOrientation = 3.1
            , double prmIntervalOrientation = 0.2
            , double prmToleranceRateOfBarlength = 0.75
            , double prmToleranceRateOfReadPoint = 0.65
            )
        {
            // パラメータの設定
            g_RowInterval = prmRowInterval;
            g_maxOrientation = prmMaxOrientation;
            g_intervalOrientation = prmIntervalOrientation;
            g_toleranceRateOfBarlength = prmToleranceRateOfBarlength;
            g_toleranceRateOfReadPoint = prmToleranceRateOfReadPoint;

            // 検出結果の格納先
            var listSHit = new List<StructHit>();

            Size size = src.Size();
            Point ptCenter = new Point(src.Cols / 2, src.Rows / 2);
            Mat srcCopy = new Mat(src, targetArea);
            Mat<byte> src8U = new Mat<byte>();

            // 画像を加工し、検出精度を上げる
            Gamma(srcCopy.Clone(), out srcCopy, 2.0);
            Cv2.CvtColor(srcCopy, srcCopy, ColorConversionCodes.BGR2GRAY);
            UnsharpMasking(srcCopy.Clone(), out srcCopy, 5.0f);
            Cv2.AdaptiveThreshold(srcCopy, srcCopy, 255, OpenCvSharp.AdaptiveThresholdTypes.GaussianC, OpenCvSharp.ThresholdTypes.Binary, 51, 20);
            opening(srcCopy, out srcCopy, 0.2f);
            srcCopy.ConvertTo(src8U, MatType.CV_8U);

            DetectBarcode(src8U, ref listSHit, "default", singleDetectMode);
            if (singleDetectMode && listSHit.Count > 0)
            {
                rtnListSHit = listSHit;
            }
            else
            {
                var listAngle = new List<double>();
                double angle = g_intervalOrientation;
                while (angle < g_maxOrientation)
                {
                    listAngle.Add(angle);
                    angle += g_intervalOrientation;
                }
                angle = -g_intervalOrientation;
                while (angle > -g_maxOrientation)
                {
                    listAngle.Add(angle);
                    angle -= g_intervalOrientation;
                }

                var listAngle2 = listAngle
                                .OrderBy(x => Math.Abs(x));

                foreach (var dAngle in listAngle2)
                {
                    double angle2 = dAngle;
                    string wLabel = angle2.ToString();

                    var trans = Cv2.GetRotationMatrix2D(ptCenter, angle2, 1.0);
                    using (var src8U_2 = src8U.Clone())
                    {
                        Cv2.WarpAffine(src8U, src8U_2, trans, size);
                        List<StructHit> listSHit2 = rotateHitList(listSHit, angle2 * -1, ptCenter);
                        DetectBarcode(src8U_2, ref listSHit2, wLabel, singleDetectMode);
                        var resultSHit2 = listSHit2.FindAll(x => x.Label == wLabel);
                        resultSHit2 = rotateHitList(resultSHit2, angle2, ptCenter);
                        if (resultSHit2.Count > 0)
                        {
                            listSHit.AddRange(resultSHit2);
                            if (singleDetectMode)
                            {
                                break;
                            }

                        }
                    }
                };
                GC.Collect();

                rtnListSHit = listSHit;
            }
            
            for(int i=0;i<rtnListSHit.Count;i++)
            {
                var tmp = rtnListSHit[i];
                tmp.rect.X += targetArea.X;
                tmp.rect.Y += targetArea.Y;
                tmp.line.x1 += targetArea.X;
                tmp.line.y1 += targetArea.Y;
                tmp.line.x2 += targetArea.X;
                tmp.line.y2 += targetArea.Y;
                rtnListSHit[i] = tmp;
            }

        }

        private static void Gamma(in OpenCvSharp.Mat src, out OpenCvSharp.Mat dst, double gamma)
        {
            byte[] LUT = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                LUT[i] = (byte)(Math.Pow((double)i / 255.0, 1.0 / gamma) * 255.0);
            }
            var lut_mat = new OpenCvSharp.Mat(1, 256, MatType.CV_8UC1, LUT);
            dst = OpenCvSharp.Mat.Zeros(src.Size(), src.Type());
            Cv2.LUT(src, lut_mat, dst);
        }

        private static void UnsharpMasking(in OpenCvSharp.Mat src, out OpenCvSharp.Mat dst, float k)
        {
            var wKernel = new float[9]
            {
            -k/9.0f,-k/9.0f,-k/9.0f,
            -k/9.0f,1+ 8*k/9.0f,-k/9.0f,
            -k/9.0f,-k/9.0f,-k/9.0f,
            };

            var kMat = new OpenCvSharp.Mat(3, 3, OpenCvSharp.MatType.CV_32F, wKernel);
            dst = OpenCvSharp.Mat.Zeros(src.Size(), src.Type());
            OpenCvSharp.Cv2.Filter2D(src, dst, -1, kMat);
        }
        private static void opening(in OpenCvSharp.Mat src, out OpenCvSharp.Mat dst, float k)
        {
            var wKernel = new float[9]
            {
            -k/9.0f,-k/9.0f,-k/9.0f,
            -k/9.0f,1+ 8*k/9.0f,-k/9.0f,
            -k/9.0f,-k/9.0f,-k/9.0f,
            };

            var kMat = new OpenCvSharp.Mat(3, 3, OpenCvSharp.MatType.CV_32F, wKernel);
            dst = src.Clone();
            OpenCvSharp.Cv2.MorphologyEx(src, dst, OpenCvSharp.MorphTypes.Open, kMat);
        }

        private static void DetectBarcode(in OpenCvSharp.Mat<byte> src, ref List<StructHit> rtnListSHit, string prmLabel, bool singleDetectMode = false)
        {

            for (int i = 0; i < src.Rows; i += g_RowInterval)
            {
                int wColPos = 0;

                while (true)
                {
                    int wStartPos = -1;
                    int wEndPos = -1;
                    int wRowPos = i;

                    DetectBar(src, wRowPos, wColPos, out wStartPos, out wEndPos);
                    if (wStartPos == -1)
                    {
                        break;
                    }

                    // バーの中央位置を取得
                    wRowPos = GetBarCenterPos(src, wRowPos, wStartPos, wEndPos);

                    if (
                        !IsInsideAtRectangleList(rtnListSHit, wStartPos, wRowPos))
                    {
                        int wStartPos2 = -1;
                        int wEndPos2 = -1;
                        DetectBar(src, wRowPos, wEndPos + 1, out wStartPos2, out wEndPos2);
                        if (wStartPos2 == -1)
                        {
                            break;
                        }

                        // バーコード判定
                        int wBarcodeStart = (wStartPos + wEndPos) / 2;
                        int wBarcodeEnd = -1;
                        int wBarcodeDiff = (wStartPos2 + wEndPos2) / 2 - wBarcodeStart;
                        //上記の差をそのまま使用すると、誤差が大きくなるので随時差を取得する。
                        double wDiff = wBarcodeDiff;

                        int wCurrentStart = wStartPos;
                        int wCurrentEnd = wEndPos;
                        int wNextStart = -1;
                        int wNextEnd = -1;

                        bool isBarcode = true;
                        for (int j = 1; j < LINES; j++)
                        {
                            wBarcodeDiff = (int)Math.Round(wDiff);
                            int wTarget = (wCurrentStart + wCurrentEnd) / 2 + wBarcodeDiff;
                            if (wTarget >= src.Cols)
                            {
                                isBarcode = false;
                                break;
                            }

                            // まず、次のバーを探す
                            DetectBar(src, wRowPos, wCurrentEnd + 1, out wNextStart, out wNextEnd);
                            if (wNextStart == -1)
                            {
                                isBarcode = false;
                                break;
                            }

                            // 次のバーの位置が、現在のバーの位置からの差と一致するかチェック
                            if (wTarget < wNextStart || wTarget > wNextEnd)
                            {
                                isBarcode = false;
                                break;
                            }

                            // 問題がなければ、差分を登録する
                            wDiff = (wDiff * j + (wNextStart + wNextEnd) / 2 - (wCurrentStart + wCurrentEnd) / 2) / (j + 1);

                            // 次のバーを現在のバーとして登録
                            wCurrentStart = wNextStart;
                            wCurrentEnd = wNextEnd;

                            //result.At<Vec3b>(i, wTarget) = new Vec3b(0, 0, 255);
                            wBarcodeEnd = wCurrentEnd;
                        }

                        int wRow = GetBarCenterPos(src, wRowPos, wBarcodeStart, wBarcodeEnd);
                        bool isUpsideDown = false;
                        if (isBarcode)
                        {
                            isBarcode = CheckBarcode(src, wRow, wBarcodeStart, wBarcodeEnd, out isUpsideDown);
                        }

                        if (isBarcode)
                        {
                            //Console.WriteLine("barcode found");
                            StructHit sHit = new StructHit();
                            sHit.line = new StructLine { x1 = wBarcodeStart, y1 = wRow, x2 = wBarcodeEnd, y2 = wRow };
                            sHit.rect = GetBarcodeRectangle(src, wRow, wBarcodeStart, wBarcodeEnd);

                            string barcodeNum = DecodeBarcode2NumString(src, sHit.line);
                            if (isUpsideDown)
                            {
                                barcodeNum = ConvertUpsideDownNumString(barcodeNum);
                            }
                            bool resultCd = false;
                            string strDecoded = DecodeNumString2String(barcodeNum, out resultCd);
                            sHit.barcodeNum = barcodeNum;
                            sHit.decoded = strDecoded;
                            sHit.Label = prmLabel;
                            sHit.isUpsiteDown = isUpsideDown;

                            if (resultCd)
                            {
                                rtnListSHit.Add(sHit);
                                wColPos = wBarcodeEnd + 1;
                                if (singleDetectMode) { return; }
                            }
                            else
                            {
                                wColPos = wEndPos;
                            }
                        }
                        else
                        {
                            wColPos = wEndPos;
                        }
                    }
                    else
                    {
                        wColPos = wEndPos;
                    }


                }
                //detectBar(dst2, i, wColPos,)
            }
        }

        /*
         * src: 入力画像
         * prmRowPos: 行の位置指定
         * prmStartPos: 検出開始位置(Col)
         * rtnStartPos: 検出されたバーの開始位置(-1の場合、検出失敗)
         * rtnEndPos: 検出されたバーの終了位置(-1の場合、検出失敗)
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DetectBar(in OpenCvSharp.Mat<byte> src, int prmRowPos, int prmStartPos, out int rtnStartPos, out int rtnEndPos)
        {
            rtnStartPos = -1;
            rtnEndPos = -1;
            int wCols = src.Cols;

            unsafe
            {
                byte* ptr = src.DataPointer;
                for (int i = prmStartPos; i < wCols; i++)
                {
                    //if (src.At<byte>(prmRowPos, i) == 0)
                    if (ptr[prmRowPos * wCols + i] == 0)
                    {
                        if (rtnStartPos == -1)
                        {
                            rtnStartPos = i;
                        }
                    }
                    else
                    {
                        if (rtnStartPos != -1)
                        {
                            rtnEndPos = i;
                            break;
                        }
                    }
                }
            }
            if (rtnEndPos == -1)
            {
                rtnStartPos = -1;
            }
        }

        /*
         * src: 入力画像
         * prmRowPos: 行の位置指定
         * prmStartPos: 開始位置指定(1つ目のバーの中央位置)
         * prmEndPos: 終了位置指定(最後のバーの右端)
         * rtnIsUpsideDown: 上下反転しているかどうか
         */
        private static bool CheckBarcode(in OpenCvSharp.Mat<byte> src, int prmRowPos, int prmStartPos, int prmEndPos, out bool rtnUpsideDown)
        {
            bool result = true;
            int wTargetStart = -1;
            int wTargetEnd = -1;
            int wTargetPos = -1;
            rtnUpsideDown = false;

            // バーの縦の長さを取得 
            int wBarLength = GetBarLength(src, prmRowPos, prmStartPos, prmEndPos);
            if (wBarLength < 2)
            {
                return false;
            }

            // バーの横幅を取得
            DetectBar(src, prmRowPos, prmStartPos, out wTargetStart, out wTargetEnd);
            int wBarWidth = (wTargetEnd - wTargetStart) * 2;
            if (wBarWidth <= 0)
            {
                return false;
            }

            // バー同士の間隔を取得
            int wBarSpace = (int)Math.Round((prmEndPos - wBarWidth / 2 - prmStartPos) / (double)(LINES - 1));

            // 最終バーの長さを取得
            DetectBar(src, prmRowPos, prmEndPos - wBarWidth - 2, out wTargetStart, out wTargetEnd);
            if (wTargetStart == -1)
            {
                return false;
            }
            wTargetPos = (wTargetStart + wTargetEnd) / 2;
            int wEndBarLength = GetBarLength(src, prmRowPos, wTargetPos, prmEndPos);
            if (g_toleranceRateOfBarlength >
                    ((wBarLength > wEndBarLength) ? wEndBarLength / (double)wBarLength : wBarLength / (double)wEndBarLength)
                )
            {
                // 最初のバーと最後のバーの長さが異なる。
                return false;
            }

            // 1番目のバーの情報を取得し、整合性チェック
            wTargetPos = prmStartPos;
            if (!(src.At<byte>(prmRowPos - (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) == 0
                && src.At<byte>(prmRowPos, wTargetPos) == 0
                && src.At<byte>(prmRowPos + (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) == 0))
            {
                return false;
            }

            // 2番目のバーの情報を取得し、整合性チェック
            DetectBar(src, prmRowPos, prmStartPos + wBarWidth, out wTargetStart, out wTargetEnd);
            if (wTargetStart == -1)
            {
                return false;
            }
            wTargetPos = (wTargetStart + wTargetEnd) / 2;
            if (!(src.At<byte>(prmRowPos - (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) != 0
                && src.At<byte>(prmRowPos, wTargetPos) == 0
                && src.At<byte>(prmRowPos + (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) == 0))
            {
                // 上下が反転しているか確認する
                if (!(src.At<byte>(prmRowPos - (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) == 0
                    && src.At<byte>(prmRowPos, wTargetPos) == 0
                    && src.At<byte>(prmRowPos + (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) != 0))
                {
                    return false;
                }
                rtnUpsideDown = true;
            }

            // 最後から1つ前のバーの情報を取得し、整合性チェック
            DetectBar(src, prmRowPos, prmEndPos - wBarWidth - 2 - wBarSpace, out wTargetStart, out wTargetEnd);
            if (wTargetStart == -1)
            {
                return false;
            }
            wTargetPos = (wTargetStart + wTargetEnd) / 2;
            if (!(src.At<byte>(prmRowPos - (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) != 0
                && src.At<byte>(prmRowPos, wTargetPos) == 0
                && src.At<byte>(prmRowPos + (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) == 0))
            {
                // 上下が反転しているか確認する
                if (!(src.At<byte>(prmRowPos - (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) == 0
                    && src.At<byte>(prmRowPos, wTargetPos) == 0
                    && src.At<byte>(prmRowPos + (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) != 0
                    && rtnUpsideDown))
                {
                    rtnUpsideDown = false;
                    return false;
                }
            }

            // 最後のバーの情報を取得し、整合性チェック
            DetectBar(src, prmRowPos, prmEndPos - wBarWidth - 2, out wTargetStart, out wTargetEnd);
            if (wTargetStart == -1)
            {
                return false;
            }
            wTargetPos = (wTargetStart + wTargetEnd) / 2;
            if (!(src.At<byte>(prmRowPos - (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) == 0
                && src.At<byte>(prmRowPos, wTargetPos) == 0
                && src.At<byte>(prmRowPos + (int)Math.Round(wBarLength / 2 * g_toleranceRateOfReadPoint), wTargetPos) == 0))
            {
                return false;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBarLength(in OpenCvSharp.Mat<byte> src, int prmRowPos, int prmStartPos, int prmEndPos)
        {
            int hLength = 0;
            int lLength = 0;
            int wRows = src.Rows;
            int wCols = src.Cols;

            unsafe
            {
                byte* ptr = src.DataPointer;
                while (true)
                {
                    if (prmRowPos - hLength < 0)
                    {
                        hLength--;
                        break;
                    }
                    if (ptr[(prmRowPos - hLength) * wCols + prmStartPos] == 0)
                    {
                        hLength++;
                    }
                    else
                    {
                        break;
                    }
                }

                while (true)
                {
                    if (prmRowPos + lLength >= wRows)
                    {
                        lLength--;
                        break;
                    }
                    if (ptr[(prmRowPos + lLength) * wCols + prmStartPos] == 0)
                    {
                        lLength++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return hLength + lLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBarCenterPos(in OpenCvSharp.Mat<byte> src, int prmRowPos, int prmStartPos, int prmEndPos)
        {
            int hLength = 0;
            int lLength = 0;
            int wRows = src.Rows;
            int wCols = src.Cols;

            unsafe
            {
                byte* ptr = src.DataPointer;
                while (true)
                {
                    if (prmRowPos - hLength < 0)
                    {
                        hLength--;
                        break;
                    }
                    if (ptr[(prmRowPos - hLength) * wCols + prmStartPos] == 0)
                    {
                        hLength++;
                    }
                    else
                    {
                        break;
                    }
                }

                while (true)
                {
                    if (prmRowPos + lLength >= wRows)
                    {
                        lLength--;
                        break;
                    }
                    if (ptr[(prmRowPos + lLength) * wCols + prmStartPos] == 0)
                    {
                        lLength++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return prmRowPos + (-hLength + lLength) / 2;
        }

        /*
         * src: 入力画像
         * prmRowPos: 行の位置指定
         * prmStartPos: 開始位置指定(1つ目のバーの中央位置)
         * prmEndPos: 終了位置指定(最後のバーの右端)
         */
        private static OpenCvSharp.Rect GetBarcodeRectangle(in OpenCvSharp.Mat<byte> src, int prmRowPos, int prmStartPos, int prmEndPos)
        {
            var rtnRectalgle = new OpenCvSharp.Rect();
            int wTargetStart = -1;
            int wTargetEnd = -1;

            // 最初のバーから、バーコードの中央位置を取得
            int wCenter = GetBarCenterPos(src, prmRowPos, prmStartPos, prmEndPos);

            // バーの縦の長さを取得 
            int wBarLength = GetBarLength(src, wCenter, prmStartPos, prmEndPos);
            if (wBarLength < 2)
            {
                return rtnRectalgle;
            }

            // バーの横幅を取得
            DetectBar(src, wCenter, prmStartPos, out wTargetStart, out wTargetEnd);
            int wBarWidth = (wTargetEnd - wTargetStart) * 2;
            if (wBarWidth <= 0)
            {
                return rtnRectalgle;
            }

            int wMargin = 10;

            rtnRectalgle.X = prmStartPos - wBarWidth / 2 - wMargin;
            rtnRectalgle.Y = wCenter - wBarLength / 2 - wMargin;
            rtnRectalgle.Height = wBarLength + wMargin * 2;
            rtnRectalgle.Width = prmEndPos - rtnRectalgle.X + wMargin;

            return rtnRectalgle;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInsideAtRectangleList(in List<StructHit> prmListSHit, int prmX, int prmY)
        {
            var span = CollectionsMarshal.AsSpan<StructHit>(prmListSHit);
            var wCount = prmListSHit.Count;
            for (int i = 0; i < wCount; i++)
            {
                if (IsInsideAtRectangle(span[i].rect, prmX, prmY))
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInsideAtRectangle(in OpenCvSharp.Rect prmRec, int prmX, int prmY)
        {
            return prmRec.X <= prmX && prmRec.Y <= prmY && prmRec.X + prmRec.Width >= prmX && prmRec.Y + prmRec.Height >= prmY;
        }

        private static string DecodeBarcode2NumString(in OpenCvSharp.Mat<byte> src, StructLine prmLine)
        {
            StringBuilder sb = new StringBuilder();
            int wTargetRow = prmLine.y1;
            int wTargetStart = prmLine.x1;
            int wTargetEnd = -1;
            int wTargetPos = -1;

            // 開始位置が、バーの中央になっているので、バーの先端になるように調整
            int wPos = 0;
            while (true)
            {
                if (wTargetStart - wPos < 0)
                {
                    wPos--;
                    break;
                }

                if (src.At<byte>(wTargetRow, wTargetStart - wPos) != 0)
                {
                    wPos--;
                    break;
                }
                wPos++;
            }
            wTargetStart -= wPos;

            // 最初のバーの情報を取得
            DetectBar(src, wTargetRow, wTargetStart, out wTargetStart, out wTargetEnd);

            // バーの縦の長さを取得 
            int wBarLength = GetBarLength(src, wTargetRow, (wTargetEnd + wTargetStart) / 2, -1);
            if (wBarLength < 2)
            {
                wBarLength = 2; // ほぼ、ありえないが取得出来なかった場合はとりあえずこれを設定
            }

            for (int i = 0; i < LINES; i++)
            {
                sb.Append(DecodeBar2NumString(src, (wTargetStart + wTargetEnd) / 2, wTargetRow, wBarLength));
                DetectBar(src, wTargetRow, wTargetEnd, out wTargetStart, out wTargetEnd);
            }

            return sb.ToString();
        }
        private static string DecodeBar2NumString(in OpenCvSharp.Mat<byte> src, int prmX, int prmY, int prmBarLength)
        {
            string rtn = "";
            int high = src.At<byte>(prmY - (int)Math.Round(prmBarLength / 2 * g_toleranceRateOfReadPoint), prmX) == 0 ? 1 : 0;
            int middle = src.At<byte>(prmY, prmX) == 0 ? 1 : 0;
            int low = src.At<byte>(prmY + (int)Math.Round(prmBarLength / 2 * g_toleranceRateOfReadPoint), prmX) == 0 ? 1 : 0;

            if (high == 1 && middle == 1 && low == 1) { rtn = "1"; }
            else if (high == 1 && middle == 1 && low == 0) { rtn = "2"; }
            else if (high == 0 && middle == 1 && low == 1) { rtn = "3"; }
            else if (high == 0 && middle == 1 && low == 0) { rtn = "4"; }
            else { rtn = "0"; }

            return rtn;
        }

        private static string ConvertUpsideDownNumString(string prmNumString)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in prmNumString.ToCharArray().Reverse<char>())
            {
                switch (c)
                {
                    case '2': sb.Append('3'); break;
                    case '3': sb.Append('2'); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // Start Stop は変換しない
        private static string DecodeNumString2String(string input, out bool rtnCd)
        {
            StringBuilder sb = new StringBuilder();
            int wCd = 0;
            rtnCd = false;

            try
            {
                int ptr = 2;
                int wk = 0;
                int wPre = 0;
                string wConv = "";
                string wConvPre = "";
                while (ptr < LINES - 2 - 3)
                {
                    wk = 0;
                    wConv = " ";

                    ;
                    if (int.TryParse(input.Substring(ptr, 3), out wk) && DIC_CODE.ContainsKey(wk))
                    {
                        wConv = DIC_CODE[wk];
                        if (wConvPre == "CC1" || wConvPre == "CC2" || wConvPre == "CC3")
                        {
                            wConv = DIC_CODE[wPre * 1000 + wk];
                            wConvPre = "";
                        }
                        else if (wConv == "CC4")
                        {
                            wConv = " ";
                            wPre = 0;
                            wConvPre = "";
                        }
                        else if (wConv == "CC1" || wConv == "CC2" || wConv == "CC3")
                        {
                            wPre = wk;
                            wConvPre = wConv;
                            wConv = "";
                        }
                        else
                        {
                            wPre = 0;
                            wConvPre = "";
                        }
                    }
                    if (DIC_CODE_CD.ContainsKey(wk))
                    {
                        wCd += DIC_CODE_CD[wk];
                    }
                    sb.Append(wConv);
                    ptr += 3;
                }

                wk = 0;
                int.TryParse(input.Substring(ptr, 3), out wk);
                if (int.TryParse(input.Substring(ptr, 3), out wk) && DIC_CODE.ContainsKey(wk))
                {
                    wCd += DIC_CODE_CD[wk];
                    rtnCd = (wCd % 19 == 0);
                }
            }
            catch (Exception e)
            {
                sb = new StringBuilder();
                rtnCd = false;
            }

            return sb.ToString();
        }

        private static OpenCvSharp.Point rotatePoint(in OpenCvSharp.Point pt, double prmAngle, OpenCvSharp.Point ptCenter)
        {
            double rad = prmAngle * Math.PI / 180.0;
            double x = pt.X - ptCenter.X;
            double y = pt.Y - ptCenter.Y;
            double x2 = x * Math.Cos(rad) - y * Math.Sin(rad);
            double y2 = x * Math.Sin(rad) + y * Math.Cos(rad);
            return new OpenCvSharp.Point((int)(x2 + ptCenter.X), (int)(y2 + ptCenter.Y));
        }

        private static List<StructHit> rotateHitList(in List<StructHit> prmListSHit, double prmAngle, OpenCvSharp.Point ptCenter)
        {
            List<StructHit> rtn = new List<StructHit>();
            foreach (StructHit hit in prmListSHit)
            {
                StructHit tmp = hit;
                var pt1 = new OpenCvSharp.Point(tmp.line.x1, tmp.line.y1);
                var pt2 = new OpenCvSharp.Point(tmp.line.x2, tmp.line.y2);
                pt1 = rotatePoint(pt1, prmAngle, ptCenter);
                pt2 = rotatePoint(pt2, prmAngle, ptCenter);
                tmp.line.x1 = pt1.X;
                tmp.line.y1 = pt1.Y;
                tmp.line.x2 = pt2.X;
                tmp.line.y2 = pt2.Y;

                var pt3 = new OpenCvSharp.Point(tmp.rect.X, tmp.rect.Y);
                pt3 = rotatePoint(pt3, prmAngle, ptCenter);
                tmp.rect.X = pt3.X;
                tmp.rect.Y = pt3.Y;

                rtn.Add(tmp);
            }
            return rtn;
        }

        private static void EchoLine([CallerLineNumber] int line = 0)
        {
            Console.WriteLine("Line:" + line);
        }

    }
}