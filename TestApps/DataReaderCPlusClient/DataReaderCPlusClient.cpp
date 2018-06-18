/*************************************************************************************
                                 See ReadMe.txt

    The calls (readAgilentDataIntoMemory, readAgilentDataIntoMemoryAnotherWay)
    that apply peak filtering to spectra will not work on QQQ profile data, because
    centroiding is only supported for TOF and QTOF profile data.

**************************************************************************************/

#include "stdafx.h"
#include "DataReaderCPlusClient.h"

#ifdef _DEBUG
#define new DEBUG_NEW
#endif



CWinApp theApp;

static FILE* outputFp = stdout;
using namespace std;
using namespace BDA;

long GetInstrumentTypeAndSourceType( CComPtr<IMsdrDataReader> pMSDataReader );
int readAgilentDataIntoMemory(CComPtr<IMsdrDataReader> pMSDataReader );
void printSpectrumPoints(CComPtr<BDA::IBDASpecData> pSpecData, long specId);
int readAgilentDataIntoMemoryAnotherWay(int dataPoints, CComPtr<IMsdrDataReader> pMSDataReader );
int readDataWithCustomChromFilter(CComPtr<IMsdrDataReader> pMSDataReader );
void ExtractNonMSData();
void DoSpectrumExtraction(CComPtr<IMsdrDataReader> pMSDataReader);
void GetAllScanRecords(CComPtr<IMsdrDataReader> pMSDataReader);
void ExtractEIC(CComPtr<IMsdrDataReader> pMSDataReader);
void GetSampleInfoData(CComPtr<IMsdrDataReader> pMSDataReader);
void TestIsPrimaryChrom();
void GetAxisInformation(CComPtr<IMsdrDataReader> pMSDataReader);

int _tmain(int argc, TCHAR* argv[], TCHAR* envp[])
{
	int nRetCode = 0;
    HRESULT hr = S_OK;

	// initialize MFC and print and error on failure
	if (!AfxWinInit(::GetModuleHandle(NULL), NULL, ::GetCommandLine(), 0))
	{
		// TODO: change error code to suit your needs
		_tprintf(_T("Fatal Error: MFC initialization failed\n"));
		nRetCode = 1;
	}
	else
	{
		// TODO: code your application's behavior here.
		CoInitialize(NULL);
		USES_CONVERSION;

		try
		{			
            CComPtr<IMsdrDataReader> pMSDataReader;

    		hr = CoCreateInstance( CLSID_MassSpecDataReader, NULL, 
                                            CLSCTX_INPROC_SERVER ,	
								            IID_IMsdrDataReader, 
                                            (void**)&pMSDataReader);

            if ( S_OK != hr )
            {
                AtlTrace(_T("ERROR - CoCreateInstance failed hr = 0x%lx"), hr);
				puts("Error");
                throw hr;
            }

            VARIANT_BOOL pRetVal = VARIANT_TRUE;
			
			

			CComBSTR bstrFilePath = "C:\\MHDAC_MIDAC_Package\\ExampleData\\BSA100_Sero100_MS1_09.d";

		    hr = pMSDataReader->OpenDataFile( bstrFilePath, &pRetVal);
		    if( FAILED(hr) )
		    {
			    puts("Failed to open data folder");
			    throw hr;
		    }	

			CComBSTR version = _T("");
			hr = pMSDataReader->get_Version(&version);
			if( FAILED(hr) )
		    {
			    puts("Failed to get API version");
			    throw hr;
		    }	
			fprintf(outputFp, "API Version: %S\n", version);

			// get the axis information
			GetAxisInformation(pMSDataReader);


            // read instrument information
            long totalDataPoints = GetInstrumentTypeAndSourceType(pMSDataReader);

            //DWORD t1, t2;
            // read the data
            //t1 = GetTickCount();
            readAgilentDataIntoMemory(pMSDataReader);
			readDataWithCustomChromFilter(pMSDataReader);            
            readAgilentDataIntoMemoryAnotherWay(totalDataPoints, pMSDataReader);
			DoSpectrumExtraction(pMSDataReader);
			GetAllScanRecords(pMSDataReader); 
			GetSampleInfoData(pMSDataReader);
			ExtractEIC(pMSDataReader);
            //t2 = GetTickCount();
            //fprintf(outputFp, "Total Time = %.2f\n", (double)((t2-t1)/1000));

		    // Close the data file
		    hr = pMSDataReader ->CloseDataFile();

            puts("Finished MS");


			//MessageBeep(MB_ICONEXCLAMATION);
			ExtractNonMSData();	

			TestIsPrimaryChrom();
            fprintf(outputFp, "\nDone.");

	    } //end try
	    catch(_com_error& e) 
	    {
		    hr = e.Error();
            fprintf(outputFp, "\nException");

	    }
	    catch(HRESULT& hr) 
	    {
		    _com_error e(hr);
		    hr = e.Error();
            fprintf(outputFp, "\nCOM Error");
	    }
	    catch(...)
	    {
		    hr = E_UNEXPECTED;
            fprintf(outputFp, "\nUnepxected error");
	    }

	    if( FAILED(hr) )
	    {
		    fprintf(outputFp,"\nError code (HRESULT) is %lx", hr);
	    }
        fprintf(outputFp, "\nPress Enter to end");
        getchar();

    }// end else

	CoUninitialize();
	return nRetCode;
}

long GetInstrumentTypeAndSourceType( CComPtr<IMsdrDataReader> pMSDataReader )
{
	HRESULT hr = S_OK;
    __int64 totalDataPoints = 0;

	try
    {
        // Get scan file information
		CComPtr<BDA::IBDAMSScanFileInformation> pScanInfo;
		hr = pMSDataReader->get_MSScanFileInformation(&pScanInfo);
			
		hr = pScanInfo->get_TotalScansPresent(&totalDataPoints);

        // Get device type
		enum DeviceType devType;
		hr = pScanInfo ->get_DeviceType(&devType);

	    fprintf (outputFp, "Device Type: %d\n", devType );
		
		// get IonModes
		enum IonizationMode ionMode;
		hr = pScanInfo->get_IonModes(&ionMode);

		CComBSTR sourceType = _T("");
		switch ( ionMode )
		{
			// TODO: This code is wrong - this bit flag enums
			// so you need to do OR's
        case IonizationMode_Mixed:
				sourceType = _T("Mixed");
				break;
			case IonizationMode_EI:
				sourceType = _T("EI");
				break;
			case IonizationMode_CI:
				sourceType = _T("CI");
				break;
			case IonizationMode_Maldi:
				sourceType = _T("Maldi");
				break;
			case IonizationMode_Appi:
				sourceType = _T("Appi");
				break;
			case IonizationMode_Apci:
				sourceType = _T("Apci");
				break;
			case IonizationMode_Esi:
				sourceType = _T("Esi");
				break;
			default:
				break;
		}

		// TODO: Find the valid Ion source for QTOF
		// TODO: Do an uppercase on the type
		// For now, just print out the ion source
		if (sourceType != _T(""))
		{
			fprintf (outputFp, "Ion Source Type: %S\n", sourceType );
		}
		else
		{
			fprintf (outputFp, "Ion Source is unspecified\n");
		}       

	} //end try
	catch(_com_error& e) 
	{
		hr = e.Error();
	}
	catch(HRESULT& hr) 
	{
		_com_error e(hr);
		hr = e.Error();
	}
	catch(...)
	{
		hr = E_UNEXPECTED;
	}

	if( FAILED(hr) )
	{
		fprintf(outputFp,"Error code (HRESULT) is %lx\n", hr);
	}

	return (long)totalDataPoints;
}
//This function illustrates how to create a INonmsDataReader object and 
//use it to query for NonMS data that match the criteria specified.
void ExtractNonMSData()
{
	HRESULT hr = S_OK;	

			
            CComPtr<IMsdrDataReader> pMSDataReader;

    		hr = CoCreateInstance( CLSID_MassSpecDataReader, NULL, 
                                            CLSCTX_INPROC_SERVER ,	
								            IID_IMsdrDataReader, 
                                            (void**)&pMSDataReader);

	if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }
	
	VARIANT_BOOL pRetVal = VARIANT_TRUE;
	CComBSTR bstrFilePath = "C:\\MHDAC_MIDAC_Package\\ExampleData\\TOFsulfasMS4GHzDualMode+DADSpectra+UVSignal272.d";
	hr = pMSDataReader->OpenDataFile( bstrFilePath, &pRetVal);		
    if( FAILED(hr) )
    {
	    puts("Failed to open data folder");
	    throw hr;
    }

	CComQIPtr<INonmsDataReader> nonMsReader;
	pMSDataReader->QueryInterface( IID_INonmsDataReader, (void**)&nonMsReader);
	//Get all devices and their corresponding information		
	SAFEARRAY* pNonMSDeviceArray;
	hr = nonMsReader->GetNonmsDevices(&pNonMSDeviceArray);
	if ( S_OK != hr )
	{
		fprintf(outputFp, "Error getting Devices.\n");
		fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);		
	}

	//how many elements in pNonMSDeviceArray?
	long upper = 0;
    hr = SafeArrayGetUBound(pNonMSDeviceArray, 1, &upper);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx\n", hr);		
	}

	long lower = 0;
    hr = SafeArrayGetLBound(pNonMSDeviceArray, 1, &lower);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetLBound failed 0x%lx\n", hr);
	}

	IDeviceInfo** devices = NULL;
	SafeArrayAccessData(pNonMSDeviceArray, reinterpret_cast<void**>(&devices));
    SafeArrayUnaccessData(pNonMSDeviceArray);
	DeviceType devType;
	for(; lower<=upper; lower++)
	{
		//iterate over each devices
		IDeviceInfo* pDeviceData = devices[lower];
		pDeviceData->get_DeviceType(&devType);        
        if ( devType ==  DeviceType_DiodeArrayDetector )
        {            
			SAFEARRAY* psigInfoArray;
			hr = nonMsReader->GetSignalInfo(devices[lower],StoredDataType_Chromatograms,&psigInfoArray);
			if( S_OK != hr ) 
			{
				fprintf(outputFp, "GetSignal failed 0x%lx\n", hr);
			}	
			else
			{
				ISignalInfo** signals = NULL;
				SafeArrayAccessData(psigInfoArray, reinterpret_cast<void**>(&signals));
				SafeArrayUnaccessData(psigInfoArray);
				long upperSig = 0;
				hr = SafeArrayGetUBound(psigInfoArray, 1, &upperSig);
				if( S_OK != hr) 
				{
					fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx\n", hr);
				}
				else if (upperSig == -1)
				{
					fprintf(outputFp, "ERROR - No DAD signal\n");
				}
				else
				{				
					CComPtr<BDA::IBDAChromData> pSngData;					
					ISignalInfo* pSng = signals[0];
					hr = nonMsReader ->GetSignal(pSng,&pSngData);
					if ( S_OK != hr )
					{
						fprintf(outputFp, "Error getting Signal.\n");
						fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
					}
					
					long dataPoints = 0;
					hr = pSngData ->get_TotalDataPoints(&dataPoints);
					if ( S_OK != hr )
					{
						fprintf(outputFp, "Error getting total data points from Signal.\n");
						fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
					}

					double* xArray = NULL;				
					SAFEARRAY *pRTsX = NULL;
					hr = pSngData ->get_XArray(&pRTsX);
					if ( S_OK != hr )
					{
						fprintf(outputFp, "Error getting XArray of DAD.\n");
						fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
					}

					SafeArrayAccessData(pRTsX, reinterpret_cast<void**>(&xArray));
					SafeArrayUnaccessData(pRTsX);
					double* yArray = NULL;				
					SAFEARRAY *pRTsY = NULL;
					hr = pSngData ->get_YArray(&pRTsY);
					if ( S_OK != hr )
					{
						fprintf(outputFp, "Error getting XArray of DAD.\n");
						fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
						//return(1);
					}
					SafeArrayAccessData(pRTsY, reinterpret_cast<void**>(&yArray));
					SafeArrayUnaccessData(pRTsY);
				}
			}
			//Extract TWC
			CComPtr<BDA::IBDAChromData> pChromDataTWC;			
			hr = nonMsReader ->GetTWC(devices[lower],&pChromDataTWC);
			if ( S_OK != hr )
			{
				fprintf(outputFp, "Error getting TWC.\n");
				fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			}
			else
			{
				long dataPoints = 0;
				hr = pChromDataTWC ->get_TotalDataPoints(&dataPoints);
				if ( S_OK != hr )
				{
					fprintf(outputFp, "Error getting total data points from TWC.\n");
					fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
				}
				else if (dataPoints == 0)
				{
					fprintf(outputFp, "ERROR - No TWC signal\n");
				}
				else
				{
					double* xArray = NULL;				
					SAFEARRAY *pRTsX = NULL;
					hr = pChromDataTWC ->get_XArray(&pRTsX);
					if ( S_OK != hr )
					{
						fprintf(outputFp, "Error getting XArray of TWC.\n");
						fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
					}

					SafeArrayAccessData(pRTsX, reinterpret_cast<void**>(&xArray));
					SafeArrayUnaccessData(pRTsX);
					double* yArray = NULL;				
					SAFEARRAY *pRTsY = NULL;
					hr = pChromDataTWC ->get_YArray(&pRTsY);
					if ( S_OK != hr )
					{
						fprintf(outputFp, "Error getting XArray of TWC.\n");
						fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
						//return(1);
					}
					SafeArrayAccessData(pRTsY, reinterpret_cast<void**>(&yArray));
					SafeArrayUnaccessData(pRTsY);	
				}
			}

			//get EWC
			CComPtr<ICenterWidthRange> rangeSignal;
			CComPtr<ICenterWidthRange> rangeRef;
			hr = CoCreateInstance( CLSID_CenterWidthRange , NULL, 
                                            CLSCTX_INPROC_SERVER ,	
								            IID_ICenterWidthRange, 
                                            (void**)&rangeSignal);
			if ( S_OK != hr )
			{
				AtlTrace(_T("ERROR - CoCreateInstance for chrom filter failed hr = 0x%lx"), hr);
				puts("Error");
				throw hr;
			}

			hr = CoCreateInstance( CLSID_CenterWidthRange , NULL, 
                                            CLSCTX_INPROC_SERVER ,	
								            IID_ICenterWidthRange, 
                                            (void**)&rangeRef);	
			if ( S_OK != hr )
			{
				AtlTrace(_T("ERROR - CoCreateInstance for chrom filter failed hr = 0x%lx"), hr);
				puts("Error");
				throw hr;
			}

			rangeSignal->put_Center(250);		
			rangeSignal->put_Width(40);
			rangeRef->put_Center(250);
			rangeRef->put_Width(40);			
			CComPtr<IRange> rangeSig;
			CComPtr<IRange> rangeReference;			
			rangeSignal->QueryInterface( IID_IRange, (void**)&rangeSig);
			rangeRef->QueryInterface( IID_IRange, (void**)&rangeReference);
			CComPtr<BDA::IBDAChromData> pChromDataEWC;			
			hr = nonMsReader ->GetEWC(devices[lower],rangeSig, rangeReference,&pChromDataEWC);
			if ( S_OK != hr )
			{
				fprintf(outputFp, "Error getting EWC.\n");
				fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
				//return(1);
			}
			else
			{
				long dataPoints = 0;
				hr = pChromDataEWC ->get_TotalDataPoints(&dataPoints);
				if ( S_OK != hr )
				{
					fprintf(outputFp, "Error getting total data points from EWC.\n");
					fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
				}
				else if (dataPoints == 0)
				{
					fprintf(outputFp, "ERROR - No EWC \n");
				}
				else
				{
					double* xArray = NULL;				
					SAFEARRAY *pRTsX = NULL;
					hr = pChromDataEWC ->get_XArray(&pRTsX);
					if ( S_OK != hr )
					{
						fprintf(outputFp, "Error getting XArray of EWC.\n");
						fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
					}

					SafeArrayAccessData(pRTsX, reinterpret_cast<void**>(&xArray));
					SafeArrayUnaccessData(pRTsX);
					double* yArray = NULL;				
					SAFEARRAY *pRTsY = NULL;
					hr = pChromDataEWC ->get_YArray(&pRTsY);
					if ( S_OK != hr )
					{
						fprintf(outputFp, "Error getting XArray of EWC.\n");
						fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
					}
					SafeArrayAccessData(pRTsY, reinterpret_cast<void**>(&yArray));
					SafeArrayUnaccessData(pRTsY);		
				}
			}
			
            //UV Spectrum
			CComPtr<ICenterWidthRange> range;
			hr = CoCreateInstance( CLSID_CenterWidthRange , NULL, 
                                            CLSCTX_INPROC_SERVER ,	
								            IID_ICenterWidthRange, 
                                            (void**)&range);
			if ( S_OK != hr )
			{
				AtlTrace(_T("ERROR - CoCreateInstance for chrom filter failed hr = 0x%lx"), hr);
				puts("Error");
				throw hr;
			}

			range->put_Center(250);		
			range->put_Width(40);
			CComPtr<IRange> rangeUV;
			range->QueryInterface( IID_IRange, (void**)&rangeUV);
			SAFEARRAY* pUVSpecData;
			hr = nonMsReader->GetUVSpectrum(devices[lower],rangeUV,&pUVSpecData);
			if ( S_OK != hr  || pUVSpecData == NULL)
			{
				fprintf(outputFp, "Error getting UV Spectrum Data.\n");
				fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);		
			}
			else
			{
				//how many elements in pUVSpecData?
				long upperUV = 0;
				hr = SafeArrayGetUBound(pUVSpecData, 1, &upper);
				if( S_OK != hr ) 
				{
					fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx\n", hr);		
				}
				
				long lowerUV = 0;
				hr = SafeArrayGetLBound(pUVSpecData, 1, &lower);
				if( S_OK != hr ) 
				{
					fprintf(outputFp, "ERROR - SafeArrayGetLBound failed 0x%lx\n", hr);
				}


				IBDASpecData** uvSpecDataArray = NULL;
				SafeArrayAccessData(pUVSpecData, reinterpret_cast<void**>(&uvSpecDataArray));
				SafeArrayUnaccessData(pUVSpecData);
				for(; lower<=upper; lower++)
				{
					//iterate over each uvData
					IBDASpecData* pUVData = uvSpecDataArray[lower];
					double* xArrayUV = NULL;				
					SAFEARRAY *pRTsX = NULL;
					hr = pUVData ->get_XArray(&pRTsX);
					if ( S_OK != hr )
					{
						fprintf(outputFp, "Error getting XArray of UV.\n");
						fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
						//return(1);
					}

					SafeArrayAccessData(pRTsX, reinterpret_cast<void**>(&xArrayUV));
					SafeArrayUnaccessData(pRTsX);
					double* yArrayUV = NULL;				
					SAFEARRAY *pRTsY = NULL;
					hr = pUVData ->get_YArray(&pRTsY);
					if ( S_OK != hr )
					{
						fprintf(outputFp, "Error getting YArray of UV.\n");
						fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
					}

					SafeArrayAccessData(pRTsY, reinterpret_cast<void**>(&yArrayUV));
					SafeArrayUnaccessData(pRTsY);
				}
			}
		}    

		//check for instrument curve - TCC		
        if ( devType ==  DeviceType_ThermostattedColumnCompartment )
        {
			SAFEARRAY* psigInfoArray;
			nonMsReader->GetSignalInfo(devices[lower],StoredDataType_InstrumentCurves,&psigInfoArray);				
			ISignalInfo** signals = NULL;
			SafeArrayAccessData(psigInfoArray, reinterpret_cast<void**>(&signals));
			SafeArrayUnaccessData(psigInfoArray);
			long upperSig = 0;
			hr = SafeArrayGetUBound(psigInfoArray, 1, &upperSig);
			if( S_OK != hr ) 
			{
				fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx\n", hr);
			}
			else
			{			
				CComPtr<BDA::IBDAChromData> pSngData;					
				ISignalInfo* pSng = signals[0];
				hr = nonMsReader ->GetSignal(pSng,&pSngData);
				if ( S_OK != hr )
				{
					fprintf(outputFp, "Error getting TCC.\n");
					fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
				}
				
				long dataPoints = 0;
				hr = pSngData ->get_TotalDataPoints(&dataPoints);
				if ( S_OK != hr )
				{
					fprintf(outputFp, "Error getting total data points from TCC.\n");
					fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
				}

				double* xArray = NULL;				
				SAFEARRAY *pRTsX = NULL;
				hr = pSngData ->get_XArray(&pRTsX);
				if ( S_OK != hr )
				{
					fprintf(outputFp, "Error getting XArray of TCC.\n");
					fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
				}

				SafeArrayAccessData(pRTsX, reinterpret_cast<void**>(&xArray));
				SafeArrayUnaccessData(pRTsX);
				double* yArray = NULL;				
				SAFEARRAY *pRTsY = NULL;
				hr = pSngData ->get_YArray(&pRTsY);
				if ( S_OK != hr )
				{
					fprintf(outputFp, "Error getting XArray of TCC.\n");
					fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
				}
				SafeArrayAccessData(pRTsY, reinterpret_cast<void**>(&yArray));
				SafeArrayUnaccessData(pRTsY);
			}
		}
	}	
	pMSDataReader->CloseDataFile(); 
}	

//This function illustrates how to create a BDAChromFilter object and 
//use it to query for chromatograms that match the criteria specified in the filter.
int readDataWithCustomChromFilter(CComPtr<IMsdrDataReader> pMSDataReader )
{
	fprintf(outputFp,"\n Reading spectra ...\n");

    HRESULT hr = S_OK;

	CComPtr<IBDAChromFilter> chromFilter;

	hr = CoCreateInstance( CLSID_BDAChromFilter, NULL, 
                                    CLSCTX_INPROC_SERVER ,	
						            IID_IBDAChromFilter, 
                                    (void**)&chromFilter);
    if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance for chrom filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }

	chromFilter->put_ChromatogramType(ChromType_TotalIon);
	chromFilter->put_DesiredMSStorageType(DesiredMSStorageType_PeakElseProfile);

	SAFEARRAY* pChromDataArray;
	hr = pMSDataReader->GetChromatogram(chromFilter, &pChromDataArray);
	if ( S_OK != hr )
	{
		fprintf(outputFp, "Error getting Chromatogram.\n");
		fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
		return(1);
	}
	//how many elements in pChromDataArray?
	long upper = 0;
    hr = SafeArrayGetUBound(pChromDataArray, 1, &upper);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx\n", hr);
		return hr;
	}
	long lower = 0;
    hr = SafeArrayGetLBound(pChromDataArray, 1, &lower);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetLBound failed 0x%lx\n", hr);
		return hr;
	}
	
	IBDAChromData** chroms = NULL;
	SafeArrayAccessData(pChromDataArray, reinterpret_cast<void**>(&chroms));
    SafeArrayUnaccessData(pChromDataArray);
	BDA::IBDAChromData* pChromData = chroms[0];

	for(; lower<=upper; lower++)
	{//iterate over each chromatogram
		long dataPoints = 0;
		hr = pChromData ->get_TotalDataPoints(&dataPoints);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting total data points from TIC.\n");
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			return(1);
		}
		fprintf(outputFp, "Chromatogram #%d contains %d scans\n", lower, dataPoints);		
	}	
	return(0);
}

//This function shows how to get spectra and get various properties from a spectrum.
//It also shows how to make a call to deisotope a spectrum.
int readAgilentDataIntoMemory(CComPtr<IMsdrDataReader> pMSDataReader )
{
    fprintf(outputFp,"\n Reading spectra ...\n");

    HRESULT hr = S_OK;

	CComPtr<BDA::IBDAChromData> pChromData;	
	hr = pMSDataReader ->GetTIC(&pChromData);
	if ( S_OK != hr )
	{
		fprintf(outputFp, "Error getting TIC.\n");
		fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
		return(1);
	}

	long dataPoints = 0;
	hr = pChromData ->get_TotalDataPoints(&dataPoints);
	if ( S_OK != hr )
	{
		fprintf(outputFp, "Error getting total data points from TIC.\n");
		fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
		return(1);
	}

	double* xArray = NULL;
	SAFEARRAY *pRTs = NULL;
	
	hr = pChromData ->get_XArray(&pRTs);
	if ( S_OK != hr )
	{
		fprintf(outputFp, "Error getting XArray of TIC.\n");
		fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
		return(1);
	}

	SafeArrayAccessData(pRTs, reinterpret_cast<void**>(&xArray));
    SafeArrayUnaccessData(pRTs);

	//read spectral data into SpectrumMill data structures
	int numMSspectra = dataPoints;
	fprintf(outputFp,"Number of spectra: %d\n", numMSspectra);	

    CComPtr<IMsdrPeakFilter> pPeakFilter;
    CComPtr<IMsdrPeakFilter> pPeakFilter1;
    CComPtr<IMsdrPeakFilter> pPeakFilter2;

	hr = CoCreateInstance( CLSID_MsdrPeakFilter, NULL, 
                                    CLSCTX_INPROC_SERVER ,	
						            IID_IMsdrPeakFilter, 
                                    (void**)&pPeakFilter1);

    if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance for peak filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }

    pPeakFilter1->put_AbsoluteThreshold(100);
    pPeakFilter1->put_RelativeThreshold(0.1);
    pPeakFilter1->put_MaxNumPeaks(0);

	hr = CoCreateInstance( CLSID_MsdrPeakFilter, NULL, 
                                    CLSCTX_INPROC_SERVER ,	
						            IID_IMsdrPeakFilter, 
                                    (void**)&pPeakFilter2);

    if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance for peak filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }

    pPeakFilter2->put_AbsoluteThreshold(10);
    pPeakFilter2->put_RelativeThreshold(0.1);
    pPeakFilter2->put_MaxNumPeaks(0);

    DWORD t1 = GetTickCount();

    CComPtr<BDA::IBDAMSScanFileInformation> pScanInfo;
    MSLevel mslevel;
	for(long i=0; i < numMSspectra; i++)
	{
    	//fprintf(outputFp,"\nReading spectra: %d, RT = %f, ", i, xArray[i]);			

		CComPtr<BDA::IBDASpecData> pSpecData;
		int numBadApples = 0;

        pScanInfo = NULL;
        hr = pMSDataReader->GetMSScanInformation(xArray[i], &pScanInfo);
        if ( S_OK != hr)
        {
            fprintf(outputFp,"\nError reading scan info for time: %d\n", xArray[i]);
            return 1;
        }

        pScanInfo->get_MSLevel(&mslevel);

        pPeakFilter = pPeakFilter1;
        if ( mslevel ==  MSLevel_MSMS )
        {
            pPeakFilter = pPeakFilter2;
        }

		// No filters on scan type, ionpolarity, ionmode 
		// and peakfilter includes specifications on number of peaks, thresholds, etc.
		hr = pMSDataReader ->GetSpectrum(xArray[i], MSScanType_All, IonPolarity_Mixed , 
                                          IonizationMode_Unspecified, pPeakFilter, &pSpecData);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting spectrum #%ld.\n", i);
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);            
            return 1;
        }

		////Shows how to call ConvertDataToMassUnits()
		//hr = pSpecData->ConvertDataToMassUnits();
		//if(S_OK != hr)
		//{
		//	fprintf(outputFp, "Convert to mass unit error");
		//}		

		// Convert the ISpecData to be IScanDetails
		enum MSLevel msLevel;
		hr = pSpecData ->get_MSLevelInfo( &msLevel );
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting MSlevel for spectrum #%ld.\n", i);
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			return(1);
		}

        IRange *pRange;
        pSpecData->get_MeasuredMassRange(&pRange);
        double start = 0, end = 0;
        if ( NULL != pRange )
        {
            pRange->get_Start(&start);
            pRange->get_End(&end);
        }

        SAFEARRAY *pRetVal = NULL; 
        hr = pSpecData->get_AcquiredTimeRange(&pRetVal);
    	long lNumRecords = 0;
        hr = SafeArrayGetUBound(pRetVal, 1, &lNumRecords);
		if( S_OK != hr ) 
		{
			fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx\n", hr);
			return hr;
		}

		long lIndex = 0;
		hr = SafeArrayGetLBound(pRetVal, 1, &lIndex);
		if( S_OK != hr ) 
		{
			fprintf(outputFp, "ERROR - SafeArrayGetLBound failed 0x%lx\n", hr);
            return hr;
		}

        IRange** pRangeColUnknown;
    	SafeArrayAccessData(pRetVal, reinterpret_cast<void**>(&pRangeColUnknown));
        IRange* irangePtr = NULL;
		for (; lIndex <= lNumRecords; lIndex++ )
		{
            irangePtr = pRangeColUnknown[lIndex];
            irangePtr->get_Start(&start);
            irangePtr->get_End(&end);
        }

        SafeArrayUnaccessData(pRetVal);

        enum MSStorageMode mode; 
        pSpecData->get_MSStorageMode(&mode);

		if (msLevel == MSLevel_MS)
		{
			//fprintf(outputFp, "MSlevel = MS\t");
		}
		else if(msLevel == MSLevel_MSMS)
		{
			//fprintf(outputFp, "MSlevel = MSMS\t");
		}
		else
		{
			fprintf(outputFp, "MSlevel = %d\t", msLevel);
			throw hr = E_FAIL;
		}

        if(msLevel > MSLevel_MS)
        {
			
			//get precursor mass
			SAFEARRAY *psaPrecursors = NULL;
			double *dblArrayPrecursors = NULL;
			long precCount = 0;
			hr = pSpecData ->GetPrecursorIon(&precCount, &psaPrecursors);
			if ( S_OK != hr )
			{
				fprintf(outputFp, "Error getting Precursor Ion for spectrum #%ld.\n", i);
				fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
				return(1);
			}
			if(precCount < 1)
            {
				continue;
			}	
			//get precursor charge
			VARIANT_BOOL pRetVal = VARIANT_TRUE;
			long charge = 0;
			hr = pSpecData ->GetPrecursorCharge(&charge, &pRetVal);
			if ( S_OK != hr )
			{
				fprintf(outputFp, "Error getting Precursor Charge for spectrum #%ld.\n", i);
				return(1);
			}
			//get precursor intensity
			double intensity = 0;
			hr = pSpecData ->GetPrecursorIntensity(&intensity, &pRetVal);
			if ( S_OK != hr )
			{
				fprintf(outputFp, "Error getting Precursor intensity for spectrum #%ld.\n", i);
				return(1);
			}
			//fprintf(outputFp, "Precursor count = %d, charge = %d, intensity = %Lf\n", precCount, charge, intensity);
			//get scan ID and parent scan ID
			long scanId, parentScanId;
			hr = pSpecData ->get_ScanId(&scanId);
			if ( S_OK != hr )
			{
				fprintf(outputFp, "get ScanId error\n");
			}			
			hr = pSpecData ->get_ParentScanId(&parentScanId);
			if ( S_OK != hr )
			{
				fprintf(outputFp, "get ParentScanId error\n");
			}
//			fprintf(outputFp, "Scan Id = %ld, Parent Scan Id = %ld\n", scanId, parentScanId);
						
        }// end if

		if(i<5)
		{
			fprintf(outputFp, "Spectrum #%ld\n", i);
			
			//only print out the spectrum points for the first 5 spectra for demonstration purpose.
			printSpectrumPoints(pSpecData, i);
		}
		
		//Deisotope the spectrum
		CComPtr<IMsdrChargeStateAssignmentFilter> pCsaFilter;

		hr = CoCreateInstance( CLSID_MsdrChargeStateAssignmentFilter, NULL, 
                                    CLSCTX_INPROC_SERVER ,	
						            IID_IMsdrChargeStateAssignmentFilter, 
                                    (void**)&pCsaFilter);
		if ( S_OK != hr )
		{
			AtlTrace(_T("ERROR - CoCreateInstance for charge state assignment filter failed hr = 0x%lx"), hr);
			puts("Error");
			throw hr;
		}
		pCsaFilter->put_LimitMaxChargeState(VARIANT_TRUE);
		hr = pMSDataReader->Deisotope(pSpecData, pCsaFilter);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error in deisotoping\n");
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			return(1);
		}
		if(i<5)
		{
			//only print out the points for the first 5 deisotoped spectra for demonstration purpose
			fprintf(outputFp, "Deisotoped Spectrum #%ld\n", i);
			printSpectrumPoints(pSpecData, i);
		}
	}// end for

	DWORD t2 = GetTickCount();
	fprintf(outputFp, "Spectra Total Time = %.2f\n", (double)((t2-t1)/1000));

	//  SafeArrayUnaccessData(xArray);

	return(0);
}

void printSpectrumPoints(CComPtr<BDA::IBDASpecData> pSpecData, long specId)
{
	
	long numPeaks = 0;
	HRESULT hr = pSpecData ->get_TotalDataPoints(&numPeaks);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting number of peaks in spectrum #%ld.\n", specId);
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			return;
		}
		fprintf(outputFp, "%ld points\n", numPeaks);

		SAFEARRAY *psaX = NULL;		
		SAFEARRAY *psaY = NULL;
		hr = pSpecData ->get_XArray(&psaX);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting XArray data of spectrum #%ld.\n", specId);
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			return;
		}

        hr = pSpecData ->get_YArray(&psaY);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting YArray data of spectrum #%ld.\n", specId);
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			return;
		}
		
		//////////////////////////////////////////////////////
		/// Iterate over the spectrum points
		//////////////////////////////////////////////////////
		
		long upperBound = 0;
		hr = SafeArrayGetUBound(psaX, 1, &upperBound);
		if( S_OK != hr ) 
		{
			fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx", hr);
			return;
		}
		long lowerBound = 0;
		hr = SafeArrayGetLBound(psaX, 1, &lowerBound);
		if( S_OK != hr ) 
		{
			fprintf(outputFp, "ERROR - SafeArrayGetLBound failed 0x%lx", hr);
			return;
		}		

		double* xvals;
		float* yvals;
		SafeArrayAccessData(psaX, reinterpret_cast<void**>(&xvals));
		SafeArrayAccessData(psaY, reinterpret_cast<void**>(&yvals));
		double x;
		float y;
		for(; lowerBound<=upperBound; lowerBound++)
		{
			x=xvals[lowerBound];
			y=yvals[lowerBound];
			fprintf(outputFp, "(%Lf, %f)\n", x, y);
		}
		SafeArrayUnaccessData(psaX);
		SafeArrayUnaccessData(psaY);
}

int readAgilentDataIntoMemoryAnotherWay(int dataPoints, CComPtr<IMsdrDataReader> pMSDataReader )
{
    HRESULT hr = S_OK;

    CComPtr<BDA::IBDAActuals> pActuals;
    hr = pMSDataReader->get_ActualsInformation(&pActuals);
	if ( S_OK != hr )
	{
		fprintf(outputFp, "Error getting actuals object for the spectrum\n");
    }

	//read spectral data into SpectrumMill data structures
	int numMSspectra = dataPoints;
	fprintf(outputFp,"\nNumber of spectra: %d\n", numMSspectra);
    fprintf(outputFp,"\n Reading spectra ...");

    CComPtr<IMsdrPeakFilter> pPeakFilter1;
    CComPtr<IMsdrPeakFilter> pPeakFilter2;

	hr = CoCreateInstance( CLSID_MsdrPeakFilter, NULL, 
                                    CLSCTX_INPROC_SERVER ,	
						            IID_IMsdrPeakFilter, 
                                    (void**)&pPeakFilter1);

    if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance for peak filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }

    pPeakFilter1->put_AbsoluteThreshold(100);
    pPeakFilter1->put_RelativeThreshold(0.1);
    pPeakFilter1->put_MaxNumPeaks(0);

	hr = CoCreateInstance( CLSID_MsdrPeakFilter, NULL, 
                                    CLSCTX_INPROC_SERVER ,	
						            IID_IMsdrPeakFilter, 
                                    (void**)&pPeakFilter2);

    if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance for peak filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }

    pPeakFilter2->put_AbsoluteThreshold(10);
    pPeakFilter2->put_RelativeThreshold(0.1);
    pPeakFilter2->put_MaxNumPeaks(0);

    CComPtr<BDA::IBDAMSScanFileInformation> pScanInfo;
	for(long i=0; i < numMSspectra; i++)
	{
    	//fprintf(outputFp,"\nReading spectra: %d, RT = %f, ", i, xArray[i]);	

		CComPtr<BDA::IBDASpecData> pSpecData;
		int numBadApples = 0;

		// No filters on scan type, ionpolarity, ionmode 
		// and peakfilter includes specifications on number of peaks, thresholds, etc.
        hr = pMSDataReader ->GetSpectrum_8(i, pPeakFilter1, pPeakFilter2, 
			DesiredMSStorageType_PeakElseProfile, &pSpecData);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting spectrum #%ld.\n", i);
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);            
            return 1;
        }

        double abundancelimit = 0;
        if ( i == 0)
        {
		    hr = pSpecData ->get_AbundanceLimit( &abundancelimit );
            fprintf(outputFp, "Abundance limit = %f.\n", abundancelimit);
        }

		// Convert the ISpecData to be IScanDetails
		enum MSLevel msLevel;
		hr = pSpecData ->get_MSLevelInfo( &msLevel );
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting MSlevel for spectrum #%ld.\n", i);
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			return(1);
		}

        enum MSStorageMode mode; 
        pSpecData->get_MSStorageMode(&mode);

		if (msLevel == MSLevel_MS)
		{
			//fprintf(outputFp, "MSlevel = MS\t");
		}
		else if(msLevel == MSLevel_MSMS)
		{
			//fprintf(outputFp, "MSlevel = MSMS\t");
		}
		else
		{
			fprintf(outputFp, "MSlevel = %d\t", msLevel);
			throw hr = E_FAIL;
		}

        if(msLevel > MSLevel_MS)
        {
			
			//get precursor mass
			SAFEARRAY *psaPrecursors = NULL;
			double *dblArrayPrecursors = NULL;
			long precCount = 0;
			hr = pSpecData ->GetPrecursorIon(&precCount, &psaPrecursors);
			if ( S_OK != hr )
			{
				fprintf(outputFp, "Error getting Precursor Ion for spectrum #%ld.\n", i);
				fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
				return(1);
			}

			//fprintf(outputFp, "Precursor count = %d\t", precCount);

			if(precCount < 1)
            {
				continue;
			}		
        }// end if

		long numPeaks = 0;
//		hr = pSpecData ->get_NumPeaks(&numPeaks);
		hr = pSpecData ->get_TotalDataPoints(&numPeaks);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting number of peaks in spectrum #%ld.\n", i);
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			return(1);
		}

		SAFEARRAY *psaX = NULL;		
		SAFEARRAY *psaY = NULL;
		hr = pSpecData ->get_XArray(&psaX);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting XArray data of spectrum #%ld.\n", i);
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			return(1);
		}

        hr = pSpecData ->get_YArray(&psaY);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting YArray data of spectrum #%ld.\n", i);
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
			return(1);
		}

        SAFEARRAY *pRetVal = NULL; 
        hr = pSpecData->get_AcquiredTimeRange(&pRetVal);
    	long lNumRecords = 0;
        hr = SafeArrayGetUBound(pRetVal, 1, &lNumRecords);
		if( S_OK != hr ) 
		{
			fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx", hr);
			return hr;
		}

		long lIndex = 0;
		hr = SafeArrayGetLBound(pRetVal, 1, &lIndex);
		if( S_OK != hr ) 
		{
			fprintf(outputFp, "ERROR - SafeArrayGetLBound failed 0x%lx", hr);
            return hr;
		}

        IRange** pRangeColUnknown;
    	SafeArrayAccessData(pRetVal, reinterpret_cast<void**>(&pRangeColUnknown));
        IRange* irangePtr = NULL;
        double start = 0, end = 0;
		for (; lIndex <= lNumRecords; lIndex++ )
		{
            irangePtr = pRangeColUnknown[lIndex];
            irangePtr->get_Start(&start);
            irangePtr->get_End(&end);
        }

        SafeArrayUnaccessData(pRetVal);

        VARIANT_BOOL varbool;
        pActuals->IsActualsPresent(&varbool);
        if (varbool == VARIANT_TRUE)
        {
            SAFEARRAY *pActualArray = NULL; 
            pActuals->GetActualCollection(start,&pActualArray);
        
    	    long lNumRecords = 0;
            hr = SafeArrayGetUBound(pActualArray, 1, &lNumRecords);

            BDA::IBDAActualData** pActualColUnknown;
    	    SafeArrayAccessData(pActualArray, reinterpret_cast<void**>(&pActualColUnknown));
            BDA::IBDAActualData* iActualPtr = NULL;
            CComBSTR strName;
		    for (long lIndex = 0; lIndex <= lNumRecords; lIndex++ )
		    {
                iActualPtr = pActualColUnknown[lIndex];
                iActualPtr->get_DisplayName(&strName);
            }

            SafeArrayUnaccessData(pRetVal);
        }
    }// end for
	return(0);
}
void DoSpectrumExtraction(CComPtr<IMsdrDataReader> pMSDataReader)
{
	HRESULT hr = S_OK;	
	if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }	     		
	IRange ** scanRange = NULL;

	CComPtr<IBDASpecFilter> pSpecFilter;
			hr = CoCreateInstance( CLSID_BDASpecFilter , NULL, 
                                            CLSCTX_INPROC_SERVER ,	
								            IID_IBDASpecFilter, 
                                            (void**)&pSpecFilter);
	if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance for Spec Filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }

	pSpecFilter->put_SpectrumType(SpecType::SpecType_MassSpectrum);
	pSpecFilter->put_AverageSpectrum(true);

	// 1. create safearray for scan range
	SAFEARRAY *range;
    SAFEARRAYBOUND rgsabound[1];
	rgsabound[0].lLbound = 0;
	rgsabound[0].cElements = 1;
	long index[1];
	index[0] = 0;

    range = SafeArrayCreate(VT_DISPATCH, 1, rgsabound);
	if(range == NULL)
	{
		AtlTrace(_T("ERROR - CoCreateInstance for Spec Filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
	}

	IMinMaxRange *minmaxrange = NULL;
	hr = CoCreateInstance( CLSID_MinMaxRange , NULL, 
                                    CLSCTX_INPROC_SERVER ,	
						            IID_IMinMaxRange, 
                                    (void**)&minmaxrange);
	if ( S_OK != hr )
	{
		AtlTrace(_T("ERROR - CoCreateInstance for chrom filter failed hr = 0x%lx"), hr);
		puts("Error");
		throw hr;
	}

	minmaxrange->put_Min (2);		
	minmaxrange->put_Max(5);
	
	SafeArrayPutElement(range, index, (void*)minmaxrange);
	pSpecFilter->put_ScanRange(range);

	SAFEARRAY* pSpecDataArray;
	hr = pMSDataReader->GetSpectrum_5(pSpecFilter,&pSpecDataArray);
	
	IBDASpecData** pSpecData = NULL;
	SafeArrayAccessData(pSpecDataArray, reinterpret_cast<void**>(&pSpecData));
    SafeArrayUnaccessData(pSpecDataArray);

	//how many elements in pSpecDataArray?
	long upper = 0;
    hr = SafeArrayGetUBound(pSpecDataArray, 1, &upper);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx\n", hr);		
	}

	long lower = 0;
    hr = SafeArrayGetLBound(pSpecDataArray, 1, &lower);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetLBound failed 0x%lx\n", hr);
	}
	
	for(; lower<=upper; lower++)
	{
		IBDASpecData* pSpectrum = pSpecData[lower];	
		double* xArray = NULL;				
		SAFEARRAY *pRTsX = NULL;
		hr = pSpectrum ->get_XArray(&pRTsX);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting XArray of MS Spectrum.\n");
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);
		}
		SafeArrayAccessData(pRTsX, reinterpret_cast<void**>(&xArray));
		SafeArrayUnaccessData(pRTsX);
		double* yArray = NULL;				
		SAFEARRAY *pRTsY = NULL;
		hr = pSpectrum ->get_YArray(&pRTsY);
		if ( S_OK != hr )
		{
			fprintf(outputFp, "Error getting XArray of MS Spectrum.\n");
			fprintf(outputFp, "returned the following HRESULT: 0x%lx\n",hr);			
		}
		SafeArrayAccessData(pRTsY, reinterpret_cast<void**>(&yArray));
		SafeArrayUnaccessData(pRTsY);
	}	
	
	minmaxrange->Release();
	pSpecFilter.Release();

	SafeArrayDestroy(range);
	SafeArrayDestroy(pSpecDataArray);
}


void GetAllScanRecords(CComPtr<IMsdrDataReader> pMSDataReader)
{			
	__int64 totalDataPoints = 0;
	HRESULT hr = S_OK;
	CComPtr<BDA::IBDAMSScanFileInformation> pScanInfo;
	
	hr = pMSDataReader->get_MSScanFileInformation(&pScanInfo);
	hr = pScanInfo->get_TotalScansPresent(&totalDataPoints);
    for (int i = 0; i < totalDataPoints; i++)
    {
		CComPtr<IMSScanRecord> pMSScanRecord;

		hr = pMSDataReader->GetScanRecord(i, &pMSScanRecord);
		if(S_OK != hr)	
		{
			fprintf(outputFp, "Error getting GetScanRecord of scan %d\n",i);
		}
		else
		{
			long scanId;
			double rt;
			MSLevel mslevel;
			
			pMSScanRecord->get_ScanID(&scanId);
			pMSScanRecord->get_retentionTime(&rt);
			pMSScanRecord->get_MSLevel(&mslevel);

			fprintf (outputFp, " MSSCANRECORD - Scan id = %d\n RT = %f\n MS level = %d\n ",scanId,rt, mslevel);
		}
    }
}

void GetSampleInfoData(CComPtr<IMsdrDataReader> pMSDataReader)
{
	SAFEARRAY *pRetVal = NULL; 
	BDA::IBDASampleData* iSampleDataPtr = NULL;
	CComBSTR displayName = _T("");			
	CComBSTR displayValue = _T("");
	BDA::IBDASampleData** pSampleDataInfo;
	

	pMSDataReader->GetSampleCollection(&pRetVal);
	long lNumRecords = 0;
    HRESULT hr = SafeArrayGetUBound(pRetVal, 1, &lNumRecords);
	SafeArrayAccessData(pRetVal, reinterpret_cast<void**>(&pSampleDataInfo));

	fprintf (outputFp, "\nDISPLAYING  SAMPLE INFORMATION\n");
	for (int i = 0; i < lNumRecords; i++)
    {
		iSampleDataPtr = pSampleDataInfo[i];
		iSampleDataPtr->get_DisplayName(&displayName);
		iSampleDataPtr->get_DisplayValue(&displayValue);
		fprintf (outputFp, "%S : %S\n", displayName, displayValue);
	}

	SafeArrayUnaccessData(pRetVal);
}

void ExtractEIC(CComPtr<IMsdrDataReader> pMSDataReader)
{
	CComPtr<IBDAChromFilter> pChromFilter;
	HRESULT hr = CoCreateInstance( CLSID_BDAChromFilter , NULL, 
                                            CLSCTX_INPROC_SERVER ,	
											IID_IBDAChromFilter, 
                                            (void**)&pChromFilter);

	if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance for Chrom Filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }
	pChromFilter->put_ChromatogramType(ChromType::ChromType_ExtractedIon);
	
	SAFEARRAY *range;
    SAFEARRAYBOUND rgsabound[1];
	rgsabound[0].lLbound = 0;
	rgsabound[0].cElements = 1;

	long index[1];
	index[0] = 0;

	range = SafeArrayCreate(VT_DISPATCH, 1, rgsabound);
	if(range == NULL)
	{
		AtlTrace(_T("ERROR - CoCreateInstance for Spec Filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
	}

	ICenterWidthRange *centrewidthrange = NULL;
	hr = CoCreateInstance( CLSID_CenterWidthRange , NULL, 
                                    CLSCTX_INPROC_SERVER ,	
									IID_ICenterWidthRange, 
                                    (void**)&centrewidthrange);
	if ( S_OK != hr )
	{
		AtlTrace(_T("ERROR - CoCreateInstance for Spec filter failed hr = 0x%lx"), hr);
		puts("Error");
		throw hr;
	}

	centrewidthrange->put_Center(158);		
	centrewidthrange->put_Width(1);

	SafeArrayPutElement(range, index, (void*)centrewidthrange);
	pChromFilter->put_IncludeMassRanges(range);

	SAFEARRAY* pChromDataArray;
	hr = pMSDataReader->GetChromatogram(pChromFilter, &pChromDataArray);

	IBDAChromData** pChromData = NULL;
	SafeArrayAccessData(pChromDataArray, reinterpret_cast<void**>(&pChromData));
    SafeArrayUnaccessData(pChromDataArray);

	long up = 0;
    hr = SafeArrayGetUBound(pChromDataArray, 1, &up);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx\n", hr);		
	}

	long low = 0;
    hr = SafeArrayGetLBound(pChromDataArray, 1, &low);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetLBound failed 0x%lx\n", hr);
	}

	string strOutput;
	ChromType chromTyp;		
	SAFEARRAY* pRangeArray;
	double start;
	double end;
	long totalDataPts;
	//double abundanceLimit;

	fprintf (outputFp, "\nEXTRACTING EIC\n");
	for(; low<=up; low++)
	{
		IBDAChromData* pChrom = pChromData[low];	
		pChrom->get_ChromatogramType(&chromTyp);		
		pChrom->get_TotalDataPoints(&totalDataPts);
		
		//String* str = chromTyp;
		
		pChrom->get_AcquiredTimeRange(&pRangeArray);
		
		IRange** pRange = NULL;
		SafeArrayAccessData(pRangeArray, reinterpret_cast<void**>(&pRange));
		SafeArrayUnaccessData(pRangeArray);

		IRange* pAcqTimeRange = pRange[0];
		pAcqTimeRange->get_Start(&start);
		pAcqTimeRange->get_End(&end);		
		fprintf(outputFp, "EIC Chromatogram extracted for Range : %f to %f containing %d points\n ", start, end, totalDataPts);
	}

	centrewidthrange->Release();
	pChromFilter.Release();

	SafeArrayDestroy(range);
	SafeArrayDestroy(pChromDataArray);
}




void GetAxisInformation(CComPtr<IMsdrDataReader> pMSDataReader)
 {
	HRESULT hr = S_OK;	
	VARIANT_BOOL pRetVal = VARIANT_TRUE;
    
    CComPtr<IBDAChromFilter> pChromFilter;
	hr = CoCreateInstance( CLSID_BDAChromFilter , NULL, 
                                            CLSCTX_INPROC_SERVER ,	
											IID_IBDAChromFilter, 
                                            (void**)&pChromFilter);

	if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance for Chrom Filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }
	pChromFilter->put_ChromatogramType(ChromType::ChromType_TotalIon);

    SAFEARRAY* pChromDataArray;
	hr = pMSDataReader->GetChromatogram(pChromFilter, &pChromDataArray);

	IBDAChromData** pChromData = NULL;
	SafeArrayAccessData(pChromDataArray, reinterpret_cast<void**>(&pChromData));
    SafeArrayUnaccessData(pChromDataArray);

	long up = 0;
    hr = SafeArrayGetUBound(pChromDataArray, 1, &up);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx\n", hr);		
	}

	long low = 0;
    hr = SafeArrayGetLBound(pChromDataArray, 1, &low);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetLBound failed 0x%lx\n", hr);
	}

	DataUnit chromunit;
    DataValueType chromvalueType;

	for(; low<=up; low++)
	{
		IBDAChromData* pChrom = pChromData[low];
		hr = pChrom->GetXAxisInfoChrom(&chromunit, &chromvalueType);
		if( S_OK != hr ) 
		{
			fprintf(outputFp, "ERROR - GetXAxisInfoChrom failed 0x%lx\n", hr);		
		}

		fprintf(outputFp,"X Axis \n");
        fprintf(outputFp, "====== \n");
        fprintf(outputFp, "Data Unit:  %d \n",chromunit);
        fprintf(outputFp, "Data Value type: %d \n", chromvalueType);

		hr = pChrom->GetYAxisInfoChrom(&chromunit, &chromvalueType);
		if( S_OK != hr ) 
		{
			fprintf(outputFp, "ERROR - GetYAxisInfoChrom failed 0x%lx\n", hr);		
		}

		fprintf(outputFp,"Y Axis \n");
        fprintf(outputFp, "====== \n");
        fprintf(outputFp, "Data Unit:  %d \n",chromunit);
        fprintf(outputFp, "Data Value type: %d \n", chromvalueType);

	}
}

void TestIsPrimaryChrom()
{
    //triggered MRM file
    //Modify the path to point to a valid data file on your computer.

	HRESULT hr = S_OK;	
	VARIANT_BOOL pRetVal = VARIANT_TRUE;	

	CComPtr<IMsdrDataReader> pMSDataReader;

	hr = CoCreateInstance( CLSID_MassSpecDataReader, NULL, 
                                    CLSCTX_INPROC_SERVER ,	
						            IID_IMsdrDataReader, 
                                    (void**)&pMSDataReader);

    if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }
	CComBSTR bstrFilePath = "C:\\MHDAC_MIDAC_Package\\ExampleData\\TMRM_Mix_092110_50ppb_02.d";

    hr = pMSDataReader->OpenDataFile( bstrFilePath, &pRetVal);
    if( FAILED(hr) )
    {
	    puts("Failed to open data folder");
	    throw hr;
    }	

	CComPtr<IBDAChromFilter> pChromFilter;
	hr = CoCreateInstance( CLSID_BDAChromFilter , NULL, 
                                            CLSCTX_INPROC_SERVER ,	
											IID_IBDAChromFilter, 
                                            (void**)&pChromFilter);

	if ( S_OK != hr )
    {
        AtlTrace(_T("ERROR - CoCreateInstance for Chrom Filter failed hr = 0x%lx"), hr);
		puts("Error");
        throw hr;
    }
	pChromFilter->put_ChromatogramType(ChromType::ChromType_MultipleReactionMode);
	pChromFilter->put_MSScanTypeFilter(MSScanType::MSScanType_MultipleReaction);
	pChromFilter->put_ExtractOneChromatogramPerScanSegment(true);
	pChromFilter->put_DoCycleSum(false);

	SAFEARRAY* pChromDataArray;
	hr = pMSDataReader->GetChromatogram(pChromFilter, &pChromDataArray);

	IBDAChromData** pChromData = NULL;
	SafeArrayAccessData(pChromDataArray, reinterpret_cast<void**>(&pChromData));
    SafeArrayUnaccessData(pChromDataArray);

	long up = 0;
    hr = SafeArrayGetUBound(pChromDataArray, 1, &up);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetUBound failed 0x%lx\n", hr);		
	}

	long low = 0;
    hr = SafeArrayGetLBound(pChromDataArray, 1, &low);
	if( S_OK != hr ) 
	{
		fprintf(outputFp, "ERROR - SafeArrayGetLBound failed 0x%lx\n", hr);
	}

	SAFEARRAY* pRangeArray;
	double startMzOfInterest;
	double startMeasuredMassRange;

	for(; low<=up; low++)
	{
		IBDAChromData* pChrom = pChromData[low];
		pChrom->get_IsPrimaryMrm(&pRetVal) ;

		pChrom->get_MzOfInterest(&pRangeArray);
		
		IRange** pRange = NULL;
		SafeArrayAccessData(pRangeArray, reinterpret_cast<void**>(&pRange));
		SafeArrayUnaccessData(pRangeArray);

		IRange* mzOfInterest = pRange[0];
		mzOfInterest->get_Start(&startMzOfInterest);
		
		pChrom->get_MeasuredMassRange(&pRangeArray);
		
		pRange = NULL;
		SafeArrayAccessData(pRangeArray, reinterpret_cast<void**>(&pRange));
		SafeArrayUnaccessData(pRangeArray);

		IRange* measuredmass = pRange[0];
		measuredmass->get_Start(&startMeasuredMassRange);

		
		fprintf(outputFp, "%f -> %f, %d \n", startMzOfInterest, startMeasuredMassRange, pRetVal);
	}    
	SafeArrayDestroy( pRangeArray);
	SafeArrayDestroy(pChromDataArray);

	pMSDataReader->CloseDataFile(); 
}

