/*
 * Copyright (c) 2023 Andrew Burks
 * 
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so, subject to
 * the following conditions:
 * 
 * The above copyright notice and this permission 
 * notice shall beincluded in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
 * LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
 * WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Twinpack
{
    public class LibraryPropertyReader
    {
        public class LibraryInfo {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Author { get; set; }
            public string Company { get; set; }
            public string Version { get; set; }
        }

        const string ProjectInfoGuid = @"$11c0fc3a-9bcf-4dd8-ac38-efb93363e521";
        public static LibraryInfo Read(string libraryFile)
        {  
            var libraryInfo = new LibraryInfo(); 
            List<string> properties = getPropertyList(libraryFile); 
            libraryInfo.Name = getPropertyFromList("DefaultNameSpace", properties); 
            libraryInfo.Description = getPropertyFromList("Description", properties); 
            libraryInfo.Author = getPropertyFromList("Author", properties);
            libraryInfo.Company = getPropertyFromList("Company", properties); 
            libraryInfo.Version = getPropertyFromList("Version", properties); 
            return libraryInfo; 
        }
        
        public static string getPropertyFromList(string propertyName, List<string> propertyList) 
        { 
            int index = propertyList.FindIndex(p => String.Equals(p, propertyName, StringComparison.OrdinalIgnoreCase)) + 1;
            if (index > 0 && index < propertyList.Count) 
            {
                return propertyList[index]; 
            } 
            else
            { 
                return "";
            } 
        } 
        
        public static List<string> getPropertyList(string libraryFile)
        {
            byte[] fileData = File.ReadAllBytes(libraryFile); 
            string fileText = Encoding.ASCII.GetString(fileData); 
            int filePosition = fileText.IndexOf(ProjectInfoGuid) - 1; 
            
            if (filePosition < 0) 
            { 
                throw new Exception($"Unable to locate Project Info GUID {ProjectInfoGuid} in library file"); 
            }
            
            List<string> properties = new List<string>();
            int valueLength = 0; 
            int index = -1; 
            int nextIndex = 0;
            while (filePosition < fileData.Length - 1) 
            { 
                nextIndex = parseNumber(fileData, ref filePosition); 
                if (index + 1 != nextIndex) 
                { 
                    break; 
                } 
                
                index = nextIndex; 
                
                valueLength = parseNumber(fileData, ref filePosition); 
                if (filePosition + valueLength < fileData.Length) 
                { 
                    properties.Add(Encoding.UTF8.GetString(fileData, filePosition, valueLength)); 
                    filePosition += valueLength; 
                } 
            }
            return properties; 
        } 
        
        public static int parseNumber(byte[] buffer, ref int filePosition)       
        {           
            int value = buffer[filePosition++];           
        
            if(value >= 128)            
            {                
                value += 128 * (buffer[filePosition++] - 1);            
            }           
    
            return value;        
        }                      
    }
}
