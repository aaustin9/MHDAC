using System;
using System.IO;
using System.Collections;
using System.Linq;
using Agilent.MassSpectrometry.CommonControls.AgtFolderSelectionDialog;

namespace Agilent.MassSpectrometry.DataAnalysis.FileDialogControls
{
    public class QualFileDialogMethodOpenControl : IFileFilter, IDefaultBehaviour
    {
        #region IFileFilter Members

        private string m_FilterType = null;
        private string m_FilterString = null;
        string m_initialDir;
        string[] m_FileNamesToSelect;

        /// <summary>
        /// Gets the filter string to set in file dialog.
        /// </summary>
        public string FilterString
        {
            get
            {
                return m_FilterString;
            }
        }

        /// <summary>
        /// To return list of filtered file as per given file type.
        /// </summary>
        /// <param name="baseDirPath"></param>
        /// <returns></returns>
        public string[] GetFilteredFiles(string baseDirPath)
        {
            if (string.IsNullOrEmpty(baseDirPath)) { return null; }
            string[] filteredFiles = null;
            string[] filterExtentions = m_FilterType.Split(',');
            ArrayList filteredFilesLst = new ArrayList();
            for (int i = 0; i < filterExtentions.Length; i++)
            {
                filteredFiles = Directory.GetDirectories(baseDirPath, filterExtentions[i], SearchOption.TopDirectoryOnly);
                for (int cnt = 0; cnt < filteredFiles.Length; cnt++)
                {
                    filteredFilesLst.Add(filteredFiles[cnt]);
                }
            }
            return filteredFiles = filteredFilesLst.ToArray(Type.GetType("System.String")) as string[];
        }

        /// <summary>
        /// To set filter string and file type in file dialog.
        /// </summary>
        /// <param name="filterString"></param>
        /// <param name="filterType"></param>
        public void Initialize(string filterString, string fileType, string intialDirectory, string[] selectedFiles)
        {
            m_FilterType = fileType;
            m_FilterString = filterString;
            m_initialDir = intialDirectory;
            m_FileNamesToSelect = selectedFiles;
        }

        public string DefaultExtension
        {
            get { return ".d"; }
        }

        /// <summary>
        /// Returns the previoulsy selected files which needs to be 
        /// highlighted on file dialog
        /// </summary>
        /// <param name="initialDirectory"></param>
        /// <returns></returns>
        public string[] GetFilesForSelection(out string initialDirectory)
        {
            initialDirectory = m_initialDir;
            if (m_FileNamesToSelect != null)
            {
                return m_FileNamesToSelect.Select(f => Path.GetFileName(f)).ToArray(); ;
            }
            else
            {
                return null;
            }
        }

        #endregion
    }
}

