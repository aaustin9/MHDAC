// Define this to enable MHDAC timing tests.  
// Requires reference to MassSpecDataReader.dll, which is NOT part of the
// distributed MIDAC package
#undef ENABLE_MHDAC_TESTS

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

// For file open dialog
using Agilent.MassSpectrometry.CommonControls.AgtFolderSelectionDialog;
using Agilent.MassSpectrometry.DataAnalysis.FileDialogControls;

// For MassHunter IMS Data Access
using Agilent.MassSpectrometry.MIDAC;

namespace MidacApp
{
    public partial class Form1 : Form
    {
        IMidacImsReader m_reader;
        int m_msCount;
        bool m_doTic;
        List<IDoubleRange> m_mzRanges;
        List<IIntRange> m_ftBinRanges;
        double m_tic;
        long m_sumPoints;
        long m_sumNzPoints;

        // Items used in browsing for data files
        private string m_currentFilePath;
        private IAgtFileSelectionDialog m_dialogInterface;
        private QualFileDialogMethodOpenControl qualFileDialogOptionsControl;

        public Form1()
        {
            InitializeComponent();
            rbProfTfsMhdac.Visible = false;
#if ENABLE_MHDAC_TESTS
            rbProfTfsMhdac.Visible = true;
#endif
            rbProfTfsIter.Visible = false;
            rbFrameMsIter.Visible = false;
            rbFrameMsArrayOfIters.Visible = false;
            lbFileContents.Text = "No file is yet opened.";
            lbFileContents2.Text = "";
            gbCCS.Enabled = false;
            lbStatus.Text = "";

            // m_currentFilePath = @"D:\Data\Asms_Demo\KnitFile.d";
            m_currentFilePath = @"C:\Assembly\Test\TestData\ImsSynthChrom.d";
            m_reader = null;
            rbAllIonsHigh.Enabled = false;
            rbAllIonsLow.Enabled = false;
            rbAllIonsAll.Enabled = false;
            lbStatusAllIons.Visible = false;
#if PNNL_VERSION
            // placeholder for internal tests
            rbFrameMsArrayOfIters.Visible = true;
#endif
        }

        private void cbBrowse_Click(object sender, EventArgs e)
        {
            if (m_reader != null)
                m_reader.Close();
            m_reader = null;
            BrowseForInputFile();
        }

        private void cbOpen_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtFilePath.Text))
            {
                MessageBox.Show("No input file is specified");
                return;
            }

            string filePath = txtFilePath.Text.Trim();
            if (!Directory.Exists(filePath))
            {
                MessageBox.Show(string.Format("Folder {0} does not exist.", filePath));
                return;
            }

            if (!MidacFileAccess.FileHasImsData(filePath))
            {
                lbFileContents.Text = "File does not have IM-MS data (not opened)";
                lbFileContents2.Text = "";
                gbCCS.Enabled = false;
                return;
            }

            if (m_reader != null)
                m_reader.Close();
            m_reader = null;

            // Returns a reader alredy opened to the specified file path
            m_reader = MidacFileAccess.ImsDataReader(filePath);
            m_currentFilePath = filePath;

            // Get overall file metadata
            IMidacFileInfo fileInfo = m_reader.FileInfo;           
            
            // show something about the contents of the file
            string tfsType = "";
            if (m_reader.HasPeakTfsSpectra)
                tfsType += "Peak";
            if (m_reader.HasProfileTfsSpectra)
                tfsType += tfsType.Length > 0 ? " and Profile" : "Profile";
            
            lbFileContents.Text = string.Format("{0} frames with max of {1} drift bins/frame; with {2} Total Frame Spectra for each frame",
                fileInfo.NumFrames,
                fileInfo.MaxNonTfsMsPerFrame,
                tfsType);

            gbCCS.Enabled = m_reader.HasSingleFieldCcsInformation;
            lbFileContents2.Text = string.Format("{0} prepared for CCS conversions", m_reader.HasSingleFieldCcsInformation ? "Is" : "Is NOT");

            // If there are high/low fragmentation frames, find out how many
            if (fileInfo.HasHiLoFragData)
            {

                IMidacMsFiltersChrom chromFilters = MidacFileAccess.DefaultMsChromFilters;
                chromFilters.ApplicableFilters = ApplicableFilters.FragmentationClass;
                chromFilters.FragmentationClass = FragmentationClass.HighEnergy;
                int numHighFrames = m_reader.FilteredFrameNumbers(chromFilters).Length;
                
                chromFilters.FragmentationClass = FragmentationClass.LowEnergy;
                int numLowFrames = m_reader.FilteredFrameNumbers(chromFilters).Length;
                
                rbAllIonsAll.Enabled = true;
                rbAllIonsHigh.Enabled = true;
                rbAllIonsLow.Enabled = true;
                lbStatusAllIons.Visible = true;
                lbStatusAllIons.Text = string.Format("{0} High-CE frames; {1} Low-CE frames", numHighFrames, numLowFrames);
            }
            else
            {
                rbAllIonsAll.Enabled = false;
                rbAllIonsHigh.Enabled = false;
                rbAllIonsLow.Enabled = false;
                lbStatusAllIons.Visible = false;
            }

            // Force read of some data before we start; gets some assertions out of the way.
            IMidacUnitConverter converter = m_reader.FrameInfo(1).FrameUnitConverter;
        }

        #region File dialog support
        private void NewFileDialog(string initialDirectory)
        {
            qualFileDialogOptionsControl = new QualFileDialogMethodOpenControl();
            string[] filterString = new string[1];
            filterString[0] = initialDirectory;
            qualFileDialogOptionsControl.Initialize("Data Files(*.d)",
                "*.d", initialDirectory, filterString);
            m_dialogInterface = CreateFileDialogAndSetParameters(false, "Select .d file",
                DialogMode.Open, initialDirectory, false, qualFileDialogOptionsControl as IFileFilter);
        }

        /// <summary>
        /// Create the File Dialog and set it's basic properties
        /// </summary>
        private IAgtFileSelectionDialog CreateFileDialogAndSetParameters(bool allowMultiSelect,
            string dialogTitle,
            DialogMode dialogMode,
            string initialDirectory,
            bool showSampleInfo,
            IFileFilter iFileFilter)
        {
            AgtDialog agtDialog = new AgtDialog();
            IAgtFileSelectionDialog fileDialog = agtDialog as IAgtFileSelectionDialog;
            fileDialog.AllowMultiSelect = allowMultiSelect;
            fileDialog.HelpId = "0";
            fileDialog.DialogTitle = dialogTitle;
            fileDialog.OpenOrSave = dialogMode;
            fileDialog.InitialDirectory = initialDirectory;
            fileDialog.ShowSampleInformation = showSampleInfo;
            fileDialog.AppPlugIn = iFileFilter;
            fileDialog.Initialize(dialogMode);

            // set some basic Form properties
            agtDialog.Owner = this;
            agtDialog.ShowIcon = false;
            agtDialog.ShowInTaskbar = true;
            agtDialog.BringToFront();
            return fileDialog;
        }

        private void BrowseForInputFile()
        {
            lbStatus.Text = "";
            string currentPath = m_currentFilePath;

            if (!string.IsNullOrEmpty(currentPath))
                NewFileDialog(Path.GetDirectoryName(currentPath));

            if (m_dialogInterface.ShowDialog())
            {
                object val = m_dialogInterface.SelectedFilePaths;
                object[] files = val as object[];
                string filePath = files[0].ToString();
                NewFileDialog(Path.GetDirectoryName(filePath));
                m_currentFilePath = filePath;
                txtFilePath.Text = filePath;
                lbFileContents.Text = "File is not yet opened.";
                lbFileContents2.Text = "";
                gbCCS.Enabled = false;
                lbStatus.Text = "";
            }
        }
        #endregion

        #region Profile TFS access
        private IDoubleRange GetMzRange(string text)
        {
            if (string.IsNullOrWhiteSpace(text.Trim()))
                return null;
            string[] tokens = text.Split('-');
            if (tokens == null || tokens.Length == 0)
                return null;

            IDoubleRange mzRange = null;
            double mz1 = double.NaN;
            double mz2 = double.NaN;
            if (double.TryParse(tokens[0], out mz1))
            {
                if (tokens.Length > 1 && double.TryParse(tokens[1], out mz2))
                {
                    mzRange = new DoubleRange(mz1, mz2, MidacUnits.MassToCharge);
                }
                else
                {
                    // expand single numbers by +/- 1 amu
                    mzRange = new DoubleRange(mz1 - 0.5, mz1 + 0.5, MidacUnits.MassToCharge);
                }
            }
            return mzRange;               
        }

        private void cbReadProfileTfs_Click(object sender, EventArgs e)
        {
            if (m_reader == null)
            {
                MessageBox.Show("File is not open");
                return;
            }
            if (!m_reader.HasProfileTfsSpectra)
            {
                MessageBox.Show("This files doesn't have profile Total Frame Sepctra");
                return;
            }


            int reps = 1;
            int.TryParse(txtReps.Text, out reps);

            // Get m/z ranges
            m_doTic = rbChroTic.Checked;
            m_mzRanges = new List<IDoubleRange>();
            if (m_doTic)
            {
                double maxMz = m_reader.FileInfo.MaxFlightTimeBin;
                m_reader.FileUnitConverter.Convert(MidacUnits.FlightTimeBinIndex, MidacUnits.MassToCharge, ref maxMz);

                m_mzRanges.Add(new DoubleRange(5.0, maxMz, MidacUnits.MassToCharge));
            }
            else
            {
                IDoubleRange dr = GetMzRange(txtEic1.Text);
                if (!DoubleRange.IsNullOrEmpty(dr))
                    m_mzRanges.Add(dr);

                dr = GetMzRange(txtEic2.Text);
                if (!DoubleRange.IsNullOrEmpty(dr))
                    m_mzRanges.Add(dr);

                dr = GetMzRange(txtEic3.Text);
                if (!DoubleRange.IsNullOrEmpty(dr))
                    m_mzRanges.Add(dr);

                dr = GetMzRange(txtEic4.Text);
                if (!DoubleRange.IsNullOrEmpty(dr))
                    m_mzRanges.Add(dr);

                dr = GetMzRange(txtEic5.Text);
                if (!DoubleRange.IsNullOrEmpty(dr))
                    m_mzRanges.Add(dr);
            }

            m_ftBinRanges = new List<IIntRange>();
            IMidacUnitConverter converter = m_reader.FileUnitConverter;
            foreach (IDoubleRange mzRange in m_mzRanges)
            {
                IDoubleRange mzClone = mzRange.Clone();
                converter.Convert(MidacUnits.FlightTimeBinIndex, ref mzClone);
                m_ftBinRanges.Add(new IntRange((int)mzClone.Min, (int)mzClone.Max));                
            }
 
            try
            {
                cbBrowse.Enabled = false;
                cbOpen.Enabled = false;
                cbReadProfileTfs.Enabled = false;
                cbReadFrameMs.Enabled = false;
                gbProfileTfsFmt.Enabled = false;

                int[] frameNumbers = null;
                if(rbAllIonsHigh.Enabled)
                {
                    IMidacMsFiltersChrom chromFilters = MidacFileAccess.DefaultMsChromFilters;
                    if(rbAllIonsHigh.Checked)
                    {
                        chromFilters.ApplicableFilters = ApplicableFilters.FragmentationClass;
                        chromFilters.FragmentationClass = FragmentationClass.HighEnergy;
                    }
                    else if (rbAllIonsLow.Checked)
                    {
                        chromFilters.ApplicableFilters = ApplicableFilters.FragmentationClass;
                        chromFilters.FragmentationClass = FragmentationClass.LowEnergy;
                    }
                    frameNumbers = m_reader.FilteredFrameNumbers(chromFilters);
                }
                else
                {
                    frameNumbers = new int[m_reader.FileInfo.NumFrames];
                    for (int i = 0; i < frameNumbers.Length; i++)
                        frameNumbers[i] = i + 1;
                }

                Stopwatch sw = new Stopwatch();
                sw.Reset();

                lbStatus.Text = "Processing";
                this.Refresh();
                
                m_msCount = 0;
                m_tic = 0.0;
                m_sumPoints = 0;
                m_sumNzPoints = 0;

                for (int i = reps; i > 0; i--)
                {
                    if (rbProfTfsFrameInfo.Checked)
                        ReadFrameInfo(ref sw, frameNumbers);

#if ENABLE_MHDAC_TESTS
                    if (rbProfTfsMhdac.Checked)
                        ReadProfTfsMhdac(ref sw, frameNumbers);
#endif

                    if (rbProfTfsMidacSpecDataExternal.Checked)
                        ReadProfTfsMidacSpecDataExternal(MidacSpecFormat.Profile, ref sw, frameNumbers);

                    if (rbProfZtTfsMidacSpecDataExternal.Checked)
                        ReadProfTfsMidacSpecDataExternal(MidacSpecFormat.ZeroTrimmed, ref sw, frameNumbers);
                    
                    if (rbProfZbTfsMidacSpecDataExternal.Checked)
                        ReadProfTfsMidacSpecDataExternal(MidacSpecFormat.ZeroBounded, ref sw, frameNumbers);

                    if (rbProfMdTfsMidacSpecDataExternal.Checked)
                        ReadProfTfsMidacSpecDataExternal(MidacSpecFormat.Metadata, ref sw, frameNumbers);

                    if (rbReadTic.Checked)
                        ReadTic(ref sw, frameNumbers);                 
#if INTERNAL_TEST
                    // placeholder for internal tests
#endif
                }

                double ticks = sw.ElapsedTicks;
                double ticksPerSec = Stopwatch.Frequency;
                double milliseconds = 1000.0 * ticks / ticksPerSec / reps;
                m_tic /= reps;
                m_sumPoints /= reps;
                m_sumNzPoints /= reps;
                m_msCount /= reps;

                if (rbProfTfsFrameInfo.Checked)
                {
                    lbStatus.Text = string.Format("Done in {0:F3} ms ({1:F3} us/frame; {2} non-empty frames)",
                        milliseconds,
                        1000 * milliseconds / m_msCount,
                        m_msCount);
                }
                else if (rbReadTic.Checked)
                {
                    lbStatus.Text = string.Format("Done in {0:F3} ms; (TIC = {1})",
                        milliseconds,
                        m_tic);
                }
                else
                {
                    if (chkTfsDoSumming.Checked && !rbProfMdTfsMidacSpecDataExternal.Checked)
                    {
                        lbStatus.Text =
                            string.Format("Done in {0:##,0.###} ms ({1:##,0.###} us/frame; {2:##,0} non-empty spectra; TIC = {3:##,#}; Points = {4:##,0}; NZ = {5:##,0})",
                            milliseconds,
                            1000 * milliseconds / m_msCount,
                            m_msCount,
                            m_tic,
                            m_sumPoints,
                            m_sumNzPoints);
                    }
                    else
                    {
                        // no TIC value
                        lbStatus.Text =
                            string.Format("Done in {0:##,0.###} ms ({1:##,0.###} us/frame; {2:##,0} non-empty spectra; Points = {3:##,0}; NZ = {4:##,0})",
                            milliseconds,
                            1000 * milliseconds / m_msCount,
                            m_msCount,
                            m_sumPoints,
                            m_sumNzPoints);
                    }
                }
            }
            finally
            {
                cbBrowse.Enabled = true;
                cbOpen.Enabled = true;
                cbReadProfileTfs.Enabled = true;
                cbReadFrameMs.Enabled = true;
                gbProfileTfsFmt.Enabled = true;
            }
        }

        private void ReadProfTfsMhdac(ref Stopwatch sw, int[] frameNumbers)
        {
#if ENABLE_MHDAC_TESTS
            Agilent.MassSpectrometry.DataAnalysis.IMsdrDataReader reader = 
                new Agilent.MassSpectrometry.DataAnalysis.MassSpecDataReader();
            reader.OpenDataFile(m_currentFilePath);
            Agilent.MassSpectrometry.DataAnalysis.IBDAFileInformation fileInfo = reader.FileInformation;
            Agilent.MassSpectrometry.DataAnalysis.IBDAMSScanFileInformation msFileInfo = fileInfo.MSScanFileInformation;
            long numScans = msFileInfo.TotalScansPresent;
            // Debug.Assert((int)numScans >= numFrames, "NumScans < requested NumFrames");

            List<float[]> yArrayList = new List<float[]>(frameNumbers.Length);

            sw.Start();
            IIntRange[] xIdxRanges = null;
            int lastLength = -1;
            bool doSum = chkTfsDoSumming.Checked;
            for (int i = 0; i < frameNumbers.Length; i++)
            {
                int frameIndex = frameNumbers[i] - 1;

                // get the spectrum
                Agilent.MassSpectrometry.DataAnalysis.IBDASpecData specData = reader.GetSpectrum(frameIndex,
                    null, null, Agilent.MassSpectrometry.DataAnalysis.DesiredMSStorageType.Profile);

                // sum abundances over specified range(s)
                if (specData != null && specData.XArray != null && specData.XArray.Length > 0)
                {
                    ++m_msCount;
                    m_sumPoints += specData.XArray.Length;
                    yArrayList.Add(specData.YArray);
                    if (doSum)
                    {
                        if (specData.XArray.Length != lastLength)
                        {
                            // convert ranges to x- and y-array index equivalent
                            xIdxRanges = GetXIndexRanges(specData.XArray[0], specData.XArray.Length, m_ftBinRanges);
                            lastLength = specData.XArray.Length;
                        }

                        // sum ion abundances falling in each m/z range
                        foreach (IIntRange xIdxRange in xIdxRanges)
                        {
                            for (int j = xIdxRange.Min; j < xIdxRange.Max; j++)
                                m_tic += specData.YArray[j];
                        }
                    }
                }
            }
            sw.Stop();

           // Count nonzero points "off the clock" (MHDAC doesn't do this internally)
            foreach (float[] yArray in yArrayList)
            {
                foreach (float f in yArray)
                    if (f > 0.0f)
                        ++m_sumNzPoints;
            } 
#else
            MessageBox.Show("MHDAC tests require compiling with a reference to MassSpecDataReader.dll");
#endif
        }
     
        private void ReadFrameInfo(ref Stopwatch sw, int[] frameNumbers)
        {
            sw.Start();
            for (int i = 0; i < frameNumbers.Length; i++)
            {
                int frame = frameNumbers[i];
                IMidacFrameInfo frameInfo = m_reader.FrameInfo(frame);
                IDoubleRange acqTimeRange = frameInfo.AcqTimeRange;

                ++m_msCount;
            }

            sw.Stop();
        }

        private void ReadProfTfsMidacSpecDataExternal(MidacSpecFormat specFmt, ref Stopwatch sw, int[] frameNumbers)
        {
            sw.Start();
            int lastLength = -1;
            IIntRange[] xIdxRanges = null;
            bool doSum = chkTfsDoSumming.Checked;
            int nFrames = 0;
            int nDummy = 0;

            List<IDoubleRange> mzRanges = new List<IDoubleRange>();
            for (int i = 0; i < frameNumbers.Length; i++)
            {
                int frame = frameNumbers[i];
                double tic = 0;

                // get the TFS
                IMidacSpecDataMs specData = m_reader.ProfileTotalFrameMs(specFmt, frame);
                ++nFrames;
                if (specFmt == MidacSpecFormat.Metadata)
                {
                    if (specData != null)
                    {
                        if (specData.NonZeroPoints > 0)
                        {
                            ++m_msCount;
                            m_sumNzPoints += specData.NonZeroPoints;

                            // For testing, we don't want the maximum array size, we want the number of points
                            // actually returned -- which is zero for Metadata only calls.
                            // m_sumPoints += specData.MaxProfilePoints;
                        }
                    }
                }
                else
                {
                    if (frame == 1 || frame == 17 || frame == 18 || frame == 96 || frame == 97 || frame == 122)
                    {
                        nDummy = frame;
                    }
                    // Sum abundances over specified range(s)
                    if (specData != null && specData.XArray != null && specData.XArray.Length > 0)
                    {
                        ++m_msCount;
                        m_sumNzPoints += specData.NonZeroPoints;

                        // Again, actually returned points, not maximum number for zero-filled spectra
                        m_sumPoints += specData.XArray.Length;

                        if (doSum)
                        {
                            if (specFmt == MidacSpecFormat.Profile)
                            {
                                // We can pre-calculate array index values corresponding to the m/z 
                                // inclusion limits.
                                if (lastLength != specData.XArray.Length)
                                {
                                    // convert ranges to x- and y-array index equivalent
                                    xIdxRanges = GetXIndexRanges(specData.XArray[0], specData.XArray.Length, m_ftBinRanges);
                                    lastLength = specData.XArray.Length;
                                }

                                // sum all ion abundances falling in each m/z range
                                foreach (IIntRange xIdxRange in xIdxRanges)
                                {
                                    for (int j = xIdxRange.Min; j <= xIdxRange.Max; j++)
                                        tic += specData.YArray[j];
                                }
                            }
                            else
                            {
                                // With sparse arrays, we cannot. 
                                double[] xArray = specData.XArray;
                                float[] yArray = specData.YArray;
                                int nextIdx = 0;
                                int maxIdx = xArray.Length - 1;

                                // ToDo: if there are multiple m/z ranges, sort them in increasing m/z and 
                                // coalesce any that overlap
                                foreach (IDoubleRange mzRange in m_mzRanges)
                                {
                                    double min = mzRange.Min;
                                    double max = mzRange.Max;
                                    do
                                    {
                                        double mz = xArray[nextIdx];
                                        if (mz < min)
                                            ++nextIdx;
                                        else if (mz > max)
                                            break;
                                        else
                                            tic += yArray[nextIdx++];
                                        
                                    } while (nextIdx <= maxIdx);
                                }
                            }
                        }
                    }
                    m_tic += tic;
                }
            }
            sw.Stop();
        }

        private void ReadTic(ref Stopwatch sw, int[] frameNumbers)
        {
            IMidacMsFiltersChrom chromFilters = MidacFileAccess.DefaultMsChromFilters;
            if (rbAllIonsHigh.Enabled)
            {
                if (rbAllIonsHigh.Checked)
                {
                    chromFilters.ApplicableFilters = ApplicableFilters.FragmentationClass;
                    chromFilters.FragmentationClass = FragmentationClass.HighEnergy;
                }
                else if (rbAllIonsLow.Checked)
                {
                    chromFilters.ApplicableFilters = ApplicableFilters.FragmentationClass;
                    chromFilters.FragmentationClass = FragmentationClass.LowEnergy;
                }
            }


            sw.Start();
            IMidacChromDataMs chromData = m_reader.Chromatogram(ChromatogramType.TotalIon, false, chromFilters);
            float[] yArray = chromData.YArray;
            if (yArray != null)
            {
                for (int i = 0; i < yArray.Length; i++)
                    m_tic += yArray[i];
            }
            sw.Stop();
        }

        private IIntRange[] GetXIndexRanges(double firstMz, int xArrayLength, List<IIntRange> flightTimeBinRanges)
        {
            IIntRange[] xIdxRanges = new IIntRange[flightTimeBinRanges.Count];
            m_reader.FileUnitConverter.Convert(MidacUnits.MassToCharge, MidacUnits.FlightTimeBinIndex, ref firstMz);
            int firstMzFtBin = (int)Math.Round(firstMz);

            for (int i = 0; i < flightTimeBinRanges.Count; i++)
            {
                int minIdx = flightTimeBinRanges[i].Min - firstMzFtBin;
                int maxIdx = flightTimeBinRanges[i].Max - firstMzFtBin;
                if (minIdx < 0)
                    minIdx = 0;
                if (maxIdx >= xArrayLength)
                    maxIdx = xArrayLength - 1;
                xIdxRanges[i] = new IntRange(minIdx, maxIdx);
            }
            return xIdxRanges;
        }


        #endregion

        #region Frame MS access
        private void cbReadFrameMs_Click(object sender, EventArgs e)
        {
            if (m_reader == null)
            {
                MessageBox.Show("File is not open");
                return;
            }

            int reps = 1;
            int.TryParse(txtReps.Text, out reps);

            try
            {
                int[] frameNumbers = null;
                if (rbAllIonsHigh.Enabled)
                {
                    IMidacMsFiltersChrom chromFilters = MidacFileAccess.DefaultMsChromFilters;
                    if (rbAllIonsHigh.Checked)
                    {
                        chromFilters.ApplicableFilters = ApplicableFilters.FragmentationClass;
                        chromFilters.FragmentationClass = FragmentationClass.HighEnergy;
                    }
                    else if (rbAllIonsLow.Checked)
                    {
                        chromFilters.ApplicableFilters = ApplicableFilters.FragmentationClass;
                        chromFilters.FragmentationClass = FragmentationClass.LowEnergy;
                    }
                    frameNumbers = m_reader.FilteredFrameNumbers(chromFilters);
                }
                else
                {
                    frameNumbers = new int[m_reader.FileInfo.NumFrames];
                    for (int i = 0; i < frameNumbers.Length; i++)
                        frameNumbers[i] = i + 1;
                }

                cbBrowse.Enabled = false;
                cbOpen.Enabled = false;
                cbReadProfileTfs.Enabled = false;
                cbReadFrameMs.Enabled = false;
                gbProfileTfsFmt.Enabled = false;

                // int numFrames = m_reader.FileInfo.NumFrames;
                int numDriftBins = m_reader.FileInfo.MaxNonTfsMsPerFrame;

                Stopwatch sw = new Stopwatch();
                sw.Reset();

                lbStatus.Text = "Processing";
                this.Refresh();
                m_msCount = 0;
                m_tic = 0.0;
                m_sumPoints = 0;
                m_sumNzPoints = 0;
                ((MidacImsFileReader)m_reader).ClearCounts();

                for (int i = reps; i > 0; i--)
                {
                    if (rbFrameMsMidacSpecData.Checked)
                        ReadFrameMsMidacSpecData(MidacSpecFormat.Profile, ref sw, frameNumbers, numDriftBins);
                    
                    if (rbFrameZtMsMidacSpecData.Checked)
                        ReadFrameMsMidacSpecData(MidacSpecFormat.ZeroTrimmed, ref sw, frameNumbers, numDriftBins);

                    if (rbFrameZbMsMidacSpecData.Checked)
                        ReadFrameMsMidacSpecData(MidacSpecFormat.ZeroBounded, ref sw, frameNumbers, numDriftBins);

                    if (rbFrameMdMsMidacSpecData.Checked)
                        ReadFrameMsMidacSpecData(MidacSpecFormat.Metadata, ref sw, frameNumbers, numDriftBins);
                    
                    if (rbFrameMsReuseMidacSpecData.Checked)
                        ReadFrameMsReuseMidacSpecData(MidacSpecFormat.Profile, ref sw, frameNumbers, numDriftBins);

#if PNNL_VERSION
                    // placeholder for internal tests
                    if (rbFrameMsArrayOfIters.Checked)
                        ReadFrameMsArrayOfIters(ref sw, numFrames, numDriftBins);
#endif
                }
                ((MidacImsFileReader)m_reader).CheckCounts();

                m_msCount /= reps;
                double ticks = sw.ElapsedTicks;
                double ticksPerSec = Stopwatch.Frequency;
                double milliseconds = (1000.0 * ticks) / ticksPerSec / reps;
                m_tic /= reps;
                m_sumPoints /= reps;
                m_sumNzPoints /= reps;

                IMidacFileInfo info = m_reader.FileInfo;

                if (chkFrameMsDoSumming.Checked && !rbFrameMdMsMidacSpecData.Checked)
                {
                    lbStatus.Text =
                        string.Format("Done in {0:##,#.###} ms ({1:##,#.###} us/spectrum; {2:##,#} non-empty spectra; TIC = {3:##,#}; points = {4:##,#}; NZ = {5:##,#})",
                        milliseconds,
                        1000 * milliseconds / m_msCount,
                        m_msCount,
                        m_tic,
                        m_sumPoints,
                        m_sumNzPoints);
                }
                else
                {
                    // no TIC
                    lbStatus.Text =
                        string.Format("Done in {0:##,#.###} ms ({1:##,#.###} us/spectrum; {2:##,#} non-empty spectra; points = {3:##,#}; NZ = {4:##,#})",
                        milliseconds,
                        1000 * milliseconds / m_msCount,
                        m_msCount,
                        m_sumPoints,
                        m_sumNzPoints);

                }
            }
            finally
            {
                cbBrowse.Enabled = true;
                cbOpen.Enabled = true;
                cbReadProfileTfs.Enabled = true;
                cbReadFrameMs.Enabled = true;
                gbProfileTfsFmt.Enabled = true;
            }
        }
      
        private void ReadFrameMsMidacSpecData(MidacSpecFormat specFormat, ref Stopwatch sw, int[] frameNumbers, int numDriftBins)
        {
            sw.Start();
            int minDBin = 0;
            int maxDBin = numDriftBins - 1;

            for (int i = 0; i < frameNumbers.Length; i++)
            {
                int frame = frameNumbers[i];
                for (int dbin = minDBin; dbin <= maxDBin; dbin++)
                {
                    IMidacSpecDataMs specData = m_reader.FrameMs(frame, dbin, specFormat, true) as IMidacSpecDataMs;
                    // To access the CE range:
                    //      IDoubleRange ceRange = specData.FragmentationEnergyRange;
                    AnalyzeSpectrum(specData);
                }
            }
            sw.Stop();
        }

        private void ReadFrameMsReuseMidacSpecData(MidacSpecFormat specFormat, ref Stopwatch sw, int[] frameNumbers, int driftBins)
        {
            sw.Start();
            int minDBin = 0;
            int maxDBin = driftBins - 1;

            IMidacSpecDataMs specData = null;
            for (int i = 0; i < frameNumbers.Length; i++)
            {
                int frame = frameNumbers[i];
                for (int dbin = minDBin; dbin <= maxDBin; dbin++)
                {
                    m_reader.FrameMs(frame, dbin, specFormat, true, ref specData);
                    AnalyzeSpectrum(specData);
                }
            }
            sw.Stop();
        }

        private void ReadFrameMsArrayOfIters(ref Stopwatch sw, int numFrames, int driftBins)
        {
#if PNNL_VERSION
            sw.Start();
            int minDBin = 0;
            int maxDBin = driftBins - 1;
            Agilent.MassSpectrometry.IRlzArrayIterator[] iters = null;
            for (int frame = 1; frame <= numFrames; frame++)
            {
                m_reader.FrameMsIterators(frame, minDBin, maxDBin, out iters);
                if (iters != null)
                {
                    for (int dbin = minDBin; dbin <= maxDBin; dbin++)
                    {
                        if (dbin < iters.Length)
                            AnalyzeSpectrum(iters[dbin]);
                    }
                }
            }
            sw.Stop();
#endif
        }

        private void AnalyzeSpectrum(Agilent.MassSpectrometry.IRlzArrayIterator iter)
        {
            int pointCount = 0;
            if (iter != null)
            {
                ++m_msCount;
                int bin, abund;
                iter.Reset();
                while (iter.Next(out bin, out abund))
                {
                    m_tic += abund;
                    ++pointCount;
                }
            }
            m_sumPoints += pointCount;
            m_sumNzPoints += pointCount;
        }

        private void AnalyzeSpectrum(IMidacSpecDataMs specData)
        {
            if (specData != null)
            {
                if (specData.NonZeroPoints > 0)
                {
                    ++m_msCount;
                    m_sumNzPoints += specData.NonZeroPoints;
                }
                
                if (specData.SpectrumFormat == MidacSpecFormat.Metadata)
                {
                    // For testing purposes, we want the number of points actually returned, 
                    // not the number for a full zero-filled array.  This is zero for metadata only calls
                    // m_sumPoints += specData.MaxProfilePoints;
                }
                else if (specData.YArray != null && specData.YArray.Length > 0)
                {
                    m_sumPoints += specData.YArray.Length;
                    if (chkFrameMsDoSumming.Checked && specData.NonZeroPoints > 0)
                    {
                        foreach (float f in specData.YArray)
                            m_tic += f;
                    }
                }
            }
        }
        #endregion

        private void cbDtToCcs_Click(object sender, EventArgs e)
        {
            IImsCcsInfoReader imsCcsCalInfo = new ImsCcsInfoReader();
            imsCcsCalInfo.Read(m_currentFilePath);

            txtCcs.Text = "";

            double ionMz = 0.0;
            int ionZ = 1;
            double dtMs = 0.0;

            if (!double.TryParse(txtIonMzIn.Text, out ionMz) ||
                !double.TryParse(txtDt.Text, out dtMs) ||
                !int.TryParse(txtIonZ.Text, out ionZ))
            {
                MessageBox.Show("The ion m/z and z must be specified along with the drift time");
                return;
            }

            double ccs = imsCcsCalInfo.CcsFromDriftTime(dtMs, ionMz, ionZ);
            txtCcs.Text = string.Format("{0:F3}", ccs);
        }


        private void cbCcsToDt_Click(object sender, EventArgs e)
        {
            IImsCcsInfoReader imsCcsCalInfo = new ImsCcsInfoReader();
            imsCcsCalInfo.Read(m_currentFilePath);

            txtDt.Text = "";

            double ionMz = 0.0;
            int ionZ = 1;
            double ccs = 0.0;

            if (!double.TryParse(txtIonMzIn.Text, out ionMz) ||
                !double.TryParse(txtCcs.Text, out ccs) ||
                !int.TryParse(txtIonZ.Text, out ionZ))
            {
                MessageBox.Show("The ion m/z and z must be specified along with the CCS");
                return;
            }

            double dt = imsCcsCalInfo.DriftTimeFromCcs(ccs, ionMz, ionZ);
            txtDt.Text = string.Format("{0:F3}", dt);
        }

      
      

#if PNNL_VERSION
        #region Internal Test
        // placeholder for internal tests
        #endregion
#endif

    }
}
