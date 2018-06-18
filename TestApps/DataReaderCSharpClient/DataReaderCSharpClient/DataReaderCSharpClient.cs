using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Agilent.MassSpectrometry.DataAnalysis;

namespace DataReaderCSharpClient
{
    // ----------------------------------------   See ReadMe.txt ------------------------------------------------
    // 
    // This is a C# example that shows how to make calls through the MassHunter DataAccess API.
    // Some of the functions require certain types of data files in order to show useful information.
    // For example, GetPrecursorInfo would make more sense if the data file contains MS2 product ion scans.
    // ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The main class
    /// </summary>
    public class DataReaderCSharpClient
    {
        // 13868 "spectra", Triggered MRM
        private string _tMRM_DataFile = @"C:\MHDAC_MIDAC_Package\ExampleData\TMRM_Mix_092110_50ppb_02.d";

        // 1030 spectra, Targeted MS/MS, centroid 
        private string _tgtMsMs_DataFile = @"C:\MHDAC_MIDAC_Package\ExampleData\pepmix-with-variable-transients.d";


        // 215 MS spectra (centroid + profile), BinPump, TCC and High Dynamic Range DAD with spectra and 15 signals
        private string _nonMs_DataFile1 = @"C:\MHDAC_MIDAC_Package\ExampleData\Sulfa_Cluster_002+HDR_DAD.d";

        #region Additional data files not currently used in this example
        // 1060 spectra, MS data only, centroid
        private string _ms_DataFile = @"C:\MHDAC_MIDAC_Package\ExampleData\BSA100_Sero100_MS1_09.d";

        // 210 MS spectra (centroid + profile), TCC, DAD with spectra and one signal
        private string _nonMs_DataFile2 = @"C:\MHDAC_MIDAC_Package\ExampleData\TOFsulfasMS4GHzDualMode+DADSpectra+UVSignal272.d";

        // Synthetic IM-MS data file with 880 frames (appears as 880 spectra to MHDAC)
        private string _imms_DataFile = @"C:\MHDAC_MIDAC_Package\ExampleData\ImsSynth_Chrom.d";
        #endregion

        static void Main(string[] args)
        {

            DataReaderCSharpClient test = new DataReaderCSharpClient();
        }
        
        public DataReaderCSharpClient()
        {
            string msg = null;
            string stackTrace = null;
            try
            {
                // Transition information for MRM data files
                TestIsPrimaryChrom(_tMRM_DataFile);

                // UV spectra and signals for non-MS detectors
                ExtractNonMSData(_nonMs_DataFile1, 10);

                //Create an instance of MassSpecDataReader to open up the data file
                IMsdrDataReader dataAccess = new MassSpecDataReader();
                println("MHDA API Version: " + dataAccess.Version);
                dataAccess.OpenDataFile(_tgtMsMs_DataFile); 

                GetAxisInformation(dataAccess);
                PrintFileInfo(dataAccess);
                PrintDeviceInfo(dataAccess);
                PrintMSScanInfo(dataAccess);
                GetAllScansUsingRowIndex(dataAccess);//Get all scans using row Index
                GetPrecursorInfo(dataAccess);
                DeisotopeTest(dataAccess);
                DoSpectrumExtractionTest(dataAccess);
                DoSpectrumExtractionTestWithStorage(dataAccess);
                GetAllScanRecords(dataAccess);
                GetSampleInfoData(dataAccess);
                ExtractEIC(dataAccess);

                dataAccess.CloseDataFile();
                println("\nDone.");

            }
            catch (Exception ex)
            {
                msg = ex.Message;
                stackTrace = ex.StackTrace;

                println("Exception: " + msg);
            }
            println("Press Enter to close");
            Console.ReadLine();

        }

        /// <summary>
        /// Get all scans in the data file using row index.  The storage mode defaults to peakElseProfile.
        /// Passing null for peak filter means do not filter any peaks.
        /// </summary>
        /// <param name="dataAccess"></param>
        private void GetAllScansUsingRowIndex(IMsdrDataReader dataAccess)
        {
            long totalNumScans = dataAccess.MSScanFileInformation.TotalScansPresent;
            for (int i = 0; i < totalNumScans; i++)
            {
                IBDASpecData spec = dataAccess.GetSpectrum(i, null, null);//no peak filtering
            }
        }

        /// <summary>
        /// Get all Product Ion MS/MS scans and print out the precursor and collision energy.
        /// </summary>
        private void GetPrecursorInfo(IMsdrDataReader dataAccess)
        {            
            //Create a chromatogram filter to let the scans that match 
            //the filter criteria to pass through
            IBDAChromFilter chromFilter = new BDAChromFilter();
            chromFilter.ChromatogramType = ChromType.TotalIon;
            chromFilter.DesiredMSStorageType = DesiredMSStorageType.PeakElseProfile;//This means to use peak scans if available, otherwise use profile scans
            chromFilter.MSLevelFilter = MSLevel.MSMS;
            chromFilter.MSScanTypeFilter = MSScanType.ProductIon;
            IBDAChromData[] chroms = dataAccess.GetChromatogram(chromFilter);//expect 1 chromatogram because we're not asking it to separate by scan segments, etc.
            IBDAChromData chrom = chroms[0];

            double[] retTime = chrom.XArray;
            println("Get MS/MS spectra using retention time");
            
            for (int i = 0; i < retTime.Length; i++)
            {
                //for each retention time, get the corresponding scan
                //passing in null for peak filter means no peak threshold applied
                IBDASpecData spec = dataAccess.GetSpectrum(retTime[i], MSScanType.ProductIon, IonPolarity.Mixed, IonizationMode.Unspecified, null);
                print("RT=" + retTime[i] + ", ");
                int precursorCount = 0;
                double[] precursorIons = spec.GetPrecursorIon(out precursorCount);
                
                if (precursorCount == 1)
                {
                    print("Precursor Ions:");
                    for (int j = 0; j < precursorIons.Length; j++)
                    {
                        print(precursorIons[j] + " ");
                    }
                    println("");
                    int charge;
                    double intensity;
                    if (spec.GetPrecursorCharge(out charge))
                        println("  charge: " + charge);
                    else
                        println("  no charge avaialble");
                    if (spec.GetPrecursorIntensity(out intensity))
                        println("  intensity: " + intensity);
                    else
                        println("  no intensity available");
                }
                else
                {
                    println("No precursor ions");
                }

                /*
                //Uncomment this if you want to print out x and y values of the spectrum.
                double[] mzVals = spec.XArray;
                float[] aboundanceVals = spec.YArray;
                println("RT=" + retTime[i]);
                for (int j = 0; j < mzVals.Length; j++)
                {
                    println(mzVals[j] + ", " + aboundanceVals[j]);
                }
                 */
            }                   
        }

        /// <summary>
        /// Illustrate how to make calls to deisotope a spectrum.
        /// </summary>
        /// <param name="dataAccess"></param>
        private void DeisotopeTest(IMsdrDataReader dataAccess)
        {
            IMsdrPeakFilter filter1 = new MsdrPeakFilter();
            filter1.AbsoluteThreshold = 10;
            filter1.RelativeThreshold = 0.1;
            filter1.MaxNumPeaks = 0;//no limit on max number of peaks
            IMsdrChargeStateAssignmentFilter csaFilter = new MsdrChargeStateAssignmentFilter();

            IBDASpecFilter f = new BDASpecFilter();
            f.SpectrumType = SpecType.MassSpectrum;
                
            IBDAMSScanFileInformation msscanFileInfo = dataAccess.MSScanFileInformation;
            int numSpectra = (int)msscanFileInfo.TotalScansPresent;
            int nNonEmptyCount = 0; // just deisotope the first 10 non-empty spectra
            for (int i = 0; i < numSpectra; i++)
            {
                IBDASpecData spec = dataAccess.GetSpectrum(i, filter1, filter1);//same peak filter used for both MS and MS2 spectra (you can pass in 2 different filters)
                println("Original spectrum has " + spec.TotalDataPoints + " points");
                if (spec.XArray.Length > 0)
                    ++nNonEmptyCount;
                if (nNonEmptyCount > 10)
                    break;
                dataAccess.Deisotope(spec, csaFilter);
                println("  Deisotoped spectrum has " + spec.TotalDataPoints + " points");
            }
        }
                
        /// <summary>
        /// Illustrate how to use
        /// GetSpectrum(double retentionTime, MSScanType scanType, IonPolarity ionPolarity, IonizationMode ionMode, IMsdrPeakFilter peakFilter, bool peakFilterOnCentroid);        
        /// Get a TIC, and then use the retention time array to access each scan.
        /// </summary>
        /// <param name="dataAccess"></param>
        private void DoSpectrumExtractionTest(IMsdrDataReader dataAccess)
        {
            IMsdrPeakFilter filter1 = new MsdrPeakFilter();
            filter1.AbsoluteThreshold = 10;
            filter1.RelativeThreshold = 0.1;
            filter1.MaxNumPeaks = 0;
                       
            IBDAChromFilter chromFilter = new BDAChromFilter();
            chromFilter.ChromatogramType = ChromType.TotalIon;
            chromFilter.DesiredMSStorageType = DesiredMSStorageType.PeakElseProfile;

            IBDAChromData[] chroms = dataAccess.GetChromatogram(chromFilter);
            IBDAChromData chrom = chroms[0];

            double[] retTime = chrom.XArray;
            for (int i = 0; i < retTime.Length; i++)
            {
                println("Extract spectrum at RT=" + retTime[i]);
                //Get a spectrum without doing peak filtering if the spectrum is centroid
                //The peak filter passed in will be applied to profile spectrum, but not
                //centroid spectrum, because the flag peakFilterOnCentroid=false
                IBDASpecData spec = dataAccess.GetSpectrum(retTime[i], MSScanType.All, IonPolarity.Mixed, IonizationMode.Unspecified, filter1, false);//peakFilterOnCentroid=false
                
                /*//uncomment this section if you want to print out spectrum points
                double[] mzVals = spec.XArray;
                float[] aboundanceVals = spec.YArray;
                for (int j = 0; j < mzVals.Length; j++)
                {
                  println(mzVals[j] + ", " + aboundanceVals[j]);                   
                }
                 */
                //Get a spectrum and apply peak filtering to it regardless profile or centroid

                IBDASpecData spec2 = dataAccess.GetSpectrum(retTime[i], MSScanType.All, IonPolarity.Mixed, IonizationMode.Unspecified, filter1, true);//peakFilterOnCentroid=true
               /*
                mzVals = spec.XArray;
                aboundanceVals = spec.YArray;
                for (int j = 0; j < mzVals.Length; j++)
                {
                    if (aboundanceVals[j] > 5000)
                    {
                        println(mzVals[j] + ", " + aboundanceVals[j]);
                    }
                }
                */
                if (spec2.TotalDataPoints > spec.TotalDataPoints)
                {//since spec2 is always thresholded, it must have less than or equal # of points as spec
                    println("  Error: filtered spectrum contains more points than unfiltered spectrum!");
                }
            }
        }

        private void DoSpectrumExtractionTestWithStorage(IMsdrDataReader dataAccess)
        {
            IMsdrPeakFilter filter1 = new MsdrPeakFilter();
            filter1.AbsoluteThreshold = 10;
            filter1.RelativeThreshold = 0.1;
            filter1.MaxNumPeaks = 0;

            IBDAMSScanFileInformation msscanFileInfo = dataAccess.MSScanFileInformation;
            int numSpectra = (int)msscanFileInfo.TotalScansPresent;
            for (int i = 0; i < numSpectra; i++)
            {
                IBDASpecData spec = dataAccess.GetSpectrum(i, filter1, filter1, DesiredMSStorageType.PeakElseProfile);
                println("Spectrum " + i.ToString() + "has " + spec.TotalDataPoints + " points");
            }
        }

        private void PrintDeviceInfo(IMsdrDataReader dataAccess)
        {
            println("Device Table: ");
            DataTable table = dataAccess.FileInformation.GetDeviceTable(StoredDataType.All);
            StringBuilder buf = new StringBuilder();

            foreach (DataRow r in table.Rows)
            {
                foreach (DataColumn c in table.Columns)
                {
                    buf.AppendLine("\t" + c.ColumnName + ": " + r[c].ToString());
                }
                buf.AppendLine();
            }
            print(buf.ToString());
        }

        private void PrintMSScanInfo(IMsdrDataReader dataAccess)
        {
            //////////////////////////////////////
            ///MSScan.bin information
            /////////////////////////////////////////
            IBDAMSScanFileInformation msscan = dataAccess.FileInformation.MSScanFileInformation;
            //TotalScansPresent
            println("Total # of Scans: " + msscan.TotalScansPresent);
            //SpectraFormat
            println("Spectral Format: " + msscan.SpectraFormat);
            //is fixed cycle length?
            println("Is fixed cycle length data present? " + msscan.IsFixedCycleLengthDataPresent());
            //is multiple spectra per scan present?
            println("Is multiple spectra per scan present? " + msscan.IsMultipleSpectraPerScanPresent());
            //IonPolarity
            println("Ion Polarity: " + msscan.IonPolarity);
            //MSLevel
            println("MS Level: " + msscan.MSLevel);
            //IonModes
            println("Ionization Mode: " + msscan.IonModes);
            //DeviceType
            println("Device Type: " + msscan.DeviceType);
            //CollisionEnergy
            print("Collision Energies: ");
            double[] collisionEnergy = msscan.CollisionEnergy;
            if (collisionEnergy.Length == 0)
                println("None");
            else
            {
                for (int i = 0; i < collisionEnergy.Length; i++)
                {
                    if (i == collisionEnergy.Length - 1)//last one
                        println(Convert.ToString(collisionEnergy[i]));
                    else
                        print(Convert.ToString(collisionEnergy[i]) + ", ");
                }
            }
            //FragmentorVoltages
            print("Fragmentor Voltages: ");
            double[] fragVolt = msscan.FragmentorVoltage;
            if (fragVolt.Length == 0)
                println("None");
            else
            {
                for (int i = 0; i < fragVolt.Length; i++)
                {
                    if (i == fragVolt.Length - 1)//last one
                        println(Convert.ToString(fragVolt[i]));
                    else
                        print(Convert.ToString(fragVolt[i] + ", "));
                }
            }
            //MRMTransitions
            print("MRM transitions: ");
            IRange[] mrmTrans = msscan.MRMTransitions;
            if (mrmTrans.Length == 0)
                println("None");
            else
            {
                for (int i = 0; i < mrmTrans.Length; i++)
                {
                    if (i == mrmTrans.Length - 1)//last one
                        println(Convert.ToString(mrmTrans[i].Start) + "->" + Convert.ToString(mrmTrans[i].End));
                    else
                        print(Convert.ToString(mrmTrans[i].Start) + "->" + Convert.ToString(mrmTrans[i].End) + ", ");
                }
            }
            //SIMIons
            double[] simIons = msscan.SIMIons;
            print("SIM ions: ");
            if (simIons.Length == 0)
                println("None");
            else
            {
                for (int i = 0; i < simIons.Length; i++)
                {
                    if (i == simIons.Length - 1)
                        println(Convert.ToString(simIons[i]));
                    else
                        print(Convert.ToString(simIons[i]) + ", ");
                }
            }
            //ScanMethodNumbers
            print("Scan Method Numbers: ");
            int[] scanMethodNums = msscan.ScanMethodNumbers;
            if (scanMethodNums.Length == 0)
                println("None");
            else
            {
                for (int i = 0; i < scanMethodNums.Length; i++)
                {
                    if (i == scanMethodNums.Length - 1)//last one
                        println(Convert.ToString(scanMethodNums[i]));
                    else
                        print(Convert.ToString(scanMethodNums[i]) + ", ");
                }
            }
            //ScanTypesInformationCount
            println("Scan Type Count: " + msscan.ScanTypesInformationCount);
            //ScanTypes
            println("Scan types: " + msscan.ScanTypes);

            StringBuilder buf = new StringBuilder();
            IBDAMSScanTypeInformation[] types = msscan.GetMSScanTypeInformation();
            foreach (IBDAMSScanTypeInformation type in types)
            {
                double[] mz = type.MzOfInterest;
                buf.Append("MS scan type: " + type.MSScanType + ", " + type.IonPolarities);
                if (mz != null && mz.Length > 0)
                {
                    buf.Append(", m/z of interest = ");
                    for (int i = 0; i < mz.Length; i++)
                    {
                        if (i == mz.Length - 1)//last one
                            buf.Append(mz[i]);
                        else
                            buf.Append(mz[i] + ", ");
                    }
                }
                buf.AppendLine();
            }
            print(buf.ToString());
        }

        private void PrintFileInfo(IMsdrDataReader dataAccess)
        {
            IBDAFileInformation fileInfo = dataAccess.FileInformation;
            println("Data File: " + fileInfo.DataFileName);
            println("Acquisition Time: " + fileInfo.AcquisitionTime.ToShortDateString());
            println("IRM Status: " + fileInfo.IRMStatus);
            println("Measurement Type: " + fileInfo.MeasurementType);
            println("Separation Technique: " + fileInfo.SeparationTechnique);

            print("MS Data Present: ");
            if (fileInfo.IsMSDataPresent())
                println("yes");
            else
                println("no");

            print("Non-MS data present: ");
            if (fileInfo.IsNonMSDataPresent())
                println("yes");
            else
                println("no");

            print("UV spectra present: ");
            if (fileInfo.IsUVSpectralDataPresent())
                println("yes");
            else
                println("no");

        }

        private void print(string s)
        {
            Console.Write(s);
        }

        private void println(string s)
        {
            Console.WriteLine(s);
        }

        // Extract non-MS data from the specified file path, with UV spectra limited to at
        // most the specified number.
        private void ExtractNonMSData(string dataFilePath, int maxSpectraToReport)
        {
            IMsdrDataReader dataReader = new MassSpecDataReader();
            dataReader.OpenDataFile(dataFilePath);

            INonmsDataReader nonMsReader = dataReader as INonmsDataReader;
            IDeviceInfo[] deviceInfo = nonMsReader.GetNonmsDevices();
            if ((deviceInfo == null) || (deviceInfo.Length == 0))
            {
                println("No Non MS device found");
            }
            else
            {
                foreach (IDeviceInfo device in deviceInfo)
                {
                    println("Device = " + device.DeviceName + " Ordinal number = " + device.OrdinalNumber.ToString() + " Device type = " + device.DeviceType.ToString());

                    #region DAD Signal & Spectrum
                    if (device.DeviceType == DeviceType.DiodeArrayDetector || 
                        device.DeviceType == DeviceType.HDR ||
                        device.DeviceType == DeviceType.CompactLC1220DAD)
                    {
                        //Extracting Signal Information
                        ISignalInfo[] sigInfo = nonMsReader.GetSignalInfo(device, StoredDataType.Chromatograms);
                        if (sigInfo.Length == 0)
                        {
                            println("No DAD signal found");
                        }
                        else
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.AppendFormat("Found info on {0} signals: ", sigInfo.Length);
                            foreach (ISignalInfo info in sigInfo)
                            {
                                sb.AppendFormat("{0}, ", info.SignalName);
                            }
                            println(sb.ToString());

                            IBDAChromData chromData = nonMsReader.GetSignal(sigInfo[0]);
                            if ((chromData == null) || (chromData.TotalDataPoints == 0))
                            {
                                println("No data found for first DAD signal");
                            }
                            else
                            {
                                int count = chromData.TotalDataPoints;
                                double[] xArray = chromData.XArray;
                                float[] yArray = chromData.YArray;

                                // This information can be exported to a file on disk
                                FileStream file = new FileStream(@"C:\temp\reportNew.csv", FileMode.Create, FileAccess.ReadWrite);
                                TextWriter sw = new StreamWriter(file);
                                sw.WriteLine("#Point,X(Minutes),Y(Response Units)");
                                for (int i = 0; i < xArray.Length; i++)
                                {
                                    sw.WriteLine(i.ToString() + "," + xArray[i].ToString() + "," + yArray[i].ToString());
                                }
                                sw.Close();
                                file.Close();
                            }
                        }

                        //Extracting TWC 
                        IBDAChromData chromDataTWC = nonMsReader.GetTWC(device);

                        //compare this data with our own file
                        if ((chromDataTWC == null) || (chromDataTWC.TotalDataPoints == 0))
                        {
                            println("No TWC found");

                        }
                        else
                        {
                            int dataPoints = chromDataTWC.TotalDataPoints;
                            double[] xArrayTWC = chromDataTWC.XArray;
                            float[] yArrayTWC = chromDataTWC.YArray;

                            // Expoting this information to a file on disk
                            FileStream fileTWC = new FileStream(@"C:\temp\reportTWC.csv", FileMode.Create, FileAccess.ReadWrite);
                            TextWriter swTWC = new StreamWriter(fileTWC);
                            swTWC.WriteLine("#Point,X(Minutes),Y(Response Units)");
                            for (int i = 0; i < xArrayTWC.Length; i++)
                            {
                                swTWC.WriteLine(i.ToString() + "," + xArrayTWC[i].ToString() + "," + yArrayTWC[i].ToString());
                            }
                            swTWC.Close();
                            fileTWC.Close();
                        }

                        //Extracting EWC
                        IRange rangeSignal = new MinMaxRange(40, 250);
                        IRange rangeRef = new MinMaxRange(40, 250);

                        IBDAChromData chromDataEWC = nonMsReader.GetEWC(device, rangeSignal, rangeRef);
                        if ((chromDataEWC == null) || (chromDataEWC.TotalDataPoints == 0))
                        {
                            println("No EWC found");
                        }
                        else
                        {
                            double[] xArrayEWC = chromDataEWC.XArray;
                            float[] yArrayEWC = chromDataEWC.YArray;

                            // Expoting this information to a file on disk
                            FileStream fileEWC = new FileStream(@"C:\temp\reportEWC.csv", FileMode.Create, FileAccess.ReadWrite);
                            TextWriter swEWC = new StreamWriter(fileEWC);
                            swEWC.WriteLine("#Point,X(Minutes),Y(Response Units)");
                            for (int i = 0; i < xArrayEWC.Length; i++)
                            {
                                swEWC.WriteLine(i.ToString() + "," + xArrayEWC[i].ToString() + "," + yArrayEWC[i].ToString());
                            }
                            swEWC.Close();
                            fileEWC.Close();
                        }

                        //UV Spectrum: get each UV spectrum in the file
                        string uvSpectraFilePath = @"C:\temp\reportUV.csv";
                        if (File.Exists(uvSpectraFilePath))
                            File.Delete(uvSpectraFilePath);
                        for (int k = 0; k < chromDataTWC.XArray.Length && k < maxSpectraToReport; k++)
                        {
                            IRange range = new MinMaxRange(chromDataTWC.XArray[k], chromDataTWC.XArray[k]);

                            IBDASpecData[] uvSpecData = nonMsReader.GetUVSpectrum(device, range);
                            if (uvSpecData == null)
                            {
                                println("No UV spectrum found");
                            }
                            else
                            {
                                foreach (IBDASpecData uvSpec in uvSpecData)
                                {
                                    if (uvSpec.TotalDataPoints != 0)
                                    {
                                        int dataPopints = uvSpec.TotalDataPoints;
                                        double[] xArrayUV = uvSpec.XArray;
                                        float[] yArrayUV = uvSpec.YArray;

                                        // Expoting this information to a file on disk
                                        FileStream fileUV = new FileStream(uvSpectraFilePath, FileMode.Append,
                                                                           FileAccess.Write);
                                        TextWriter swUV = new StreamWriter(fileUV);
                                        swUV.WriteLine("#Point,X(Nanometers),Y(mAU)");
                                        for (int i = 0; i < xArrayUV.Length; i++)
                                        {
                                            swUV.WriteLine(i.ToString() + "," + xArrayUV[i].ToString() + "," +
                                                           yArrayUV[i].ToString());
                                        }
                                        swUV.Close();
                                        fileUV.Close();
                                    }
                                    else
                                    {
                                        println("No UV spectrum found");
                                    }
                                }
                            }
                        }
                    }
                    #endregion

                    #region InstrumentCurves - TCC

                    if (device.DeviceType == DeviceType.ThermostattedColumnCompartment)
                    {
                        ISignalInfo[] sigInfo = nonMsReader.GetSignalInfo(device, StoredDataType.InstrumentCurves);
                        if (sigInfo.Length == 0)
                        {
                            println("No TCC Curves found");
                        }
                        else
                        {
                            IBDAChromData chromData = nonMsReader.GetSignal(sigInfo[0]);
                            if ((chromData == null) || (chromData.TotalDataPoints == 0))
                            {
                                println("No TCC Curves found");
                            }
                            else
                            {
                                int count = chromData.TotalDataPoints;
                                double[] xArray = chromData.XArray;
                                float[] yArray = chromData.YArray;

                                // Expoting this information to a file on disk
                                FileStream file = new FileStream(@"C:\temp\reportInstrumentCurve.csv", FileMode.Create, FileAccess.ReadWrite);
                                TextWriter sw = new StreamWriter(file);
                                sw.WriteLine("#Point,X(Minutes),Y(Response Units)");
                                for (int i = 0; i < xArray.Length; i++)
                                {
                                    sw.WriteLine(i.ToString() + "," + xArray[i].ToString() + "," + yArray[i].ToString());
                                }
                                sw.Close();
                                file.Close();
                            }
                        }
                    }
                    #endregion
                }
            }
        }

        private void GetAllScanRecords(IMsdrDataReader dataAccess)
        {
            long totalNumScans = dataAccess.MSScanFileInformation.TotalScansPresent;
            for (int i = 0; i < totalNumScans; i++)
            {
                IMSScanRecord scanRecord = dataAccess.GetScanRecord(i);
                println("MSSCANRECORD - Scan ID : " + scanRecord.ScanID + " RT = " + scanRecord.RetentionTime.ToString() + " MSLevel = " + scanRecord.MSLevel.ToString());
            }
        }

        /// <summary>
        /// Display the sample information
        /// </summary>
        /// <param name="dataAccess"></param>
        private void GetSampleInfoData(IMsdrDataReader dataAccess)
        {
            IBDASampleData[] sampleData = dataAccess.GetSampleCollection();
            string strPrint = string.Empty;
            println("\nDISPLAYING SAMPLE INFORMATION DATA");
            foreach (IBDASampleData smpData in sampleData)
            {
                strPrint = strPrint + smpData.DisplayName + " : " + smpData.DataValue.ToString() + Environment.NewLine;
            }
            println(strPrint);
        }

        /// <summary>
        /// EIC Extraction
        /// </summary>
        /// <param name="dataAccess"></param>
        private void ExtractEIC(IMsdrDataReader dataAccess)
        {
            IBDAChromFilter filter = new BDAChromFilter();
            filter.ChromatogramType = ChromType.ExtractedIon;
            IRange[] ranges = new IRange[1];
            ranges[0] = new CenterWidthRange(158, 1);//EC: center mass is 158 Da, with a 1 Da window
            // ranges[0] = new MinMaxRange(157.5, 158.5);//EC: this is another way to do the same thing
            filter.IncludeMassRanges = ranges;
            IBDAChromData[] chroms = dataAccess.GetChromatogram(filter);

            println("\n EIC EXTRACTION\n");
            string chromDesc = string.Empty;
            for (int i=0 ; i< chroms.Length;i++)
            {                
                chromDesc = "Chromatogram extracted with : Chromatogram type =" + chroms[i].ChromatogramType.ToString()
                    + " and Total Datapoints = " + chroms[i].TotalDataPoints.ToString()
                    + " for range = " + chroms[i].AcquiredTimeRange[0].Start.ToString()
                    + "-" + chroms[i].AcquiredTimeRange[0].End.ToString()
                    + Environment.NewLine;
                println(chromDesc);
            }
        }

        private void GetAxisInformation(IMsdrDataReader dataAccess)
        {
            DataUnit chromunit;
            DataValueType chromvalueType;
            IBDAChromFilter filter = new BDAChromFilter();
            filter.ChromatogramType = ChromType.TotalIon;
            IBDAChromData[] chroms = dataAccess.GetChromatogram(filter);
            if (chroms.Length > 0)
            {
                chroms[0].GetXAxisInfoChrom(out chromunit, out  chromvalueType);
                println("X Axis");
                println("======");
                println("Data Unit: " + BaseUtilities.GetEnumString(chromunit, true ));
                println("Data Value type: " + BaseUtilities.GetEnumString(chromvalueType, true));

                chroms[0].GetYAxisInfoChrom(out chromunit, out  chromvalueType);
                println("Y Axis");
                println("======");
                println("Data Unit: " + BaseUtilities.GetEnumString(chromunit, true));
                println("Data Value type: " + BaseUtilities.GetEnumString(chromvalueType, true));
                println(string.Empty);

            }
        }
       
        // Examine triggered MRM data file transition chromatograms to see which are
        // for primary transitions.
        private void TestIsPrimaryChrom(string tMrmFilePath)
        {
            IMsdrDataReader dataReader = new MassSpecDataReader();
            dataReader.OpenDataFile(tMrmFilePath);
            IBDAChromFilter filter = new BDAChromFilter();
            filter.ChromatogramType = ChromType.MultipleReactionMode;
            filter.MSScanTypeFilter = MSScanType.MultipleReaction;
            filter.ExtractOneChromatogramPerScanSegment = true;
            filter.DoCycleSum = false;

            IBDAChromData[] chroms = dataReader.GetChromatogram(filter);
            foreach (IBDAChromData chrom in chroms)
            {
                bool val = chrom.IsPrimaryMrm;
                string s = string.Format("{0}->{1}, {2}", chrom.MZOfInterest[0].Start, chrom.MeasuredMassRange[0].Start,
                                         val);
                println(s);
            }

            dataReader.CloseDataFile();
        }


    }
}

