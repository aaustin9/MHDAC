========================================================================
    CONSOLE APPLICATION : DataExtractorCPlusClient Project Overview
========================================================================

This is an example of a C++ project that accesses the MassHunter Data 
Access Component (MHDAC) libraries via COM interfaces.   When run, it
opens a console window and pulls some information out of a few of the
example data files, writing some of the values it finds to the console
window.

This project can be compiled and run without changes providing that the 
MHDAC package is copied to C:, so that the high-level folder structure 
is as follows.  

    C: -+-- MHDAC_MIDAC_Package -+-- ExampleData -+-- (several *.d data folders)
                                 |
                                 +-- MHDAC_MIDAC_64bit
                                 |
                                 +-- TestApps -+-- DataReaderCPlusPlusClient -+-- (this solution) 

If the component library or data files are located elsewhere, you'll
have to make changes in this project's properties or in the data file
paths in DataReaderCPlusClient.cpp

