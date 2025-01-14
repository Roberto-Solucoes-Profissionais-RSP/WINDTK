﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace WINDTK.WXN
{
    public class WXNFile
    {
        private List<WXNPureObject> pureWriteMemory = new List<WXNPureObject>();
        private List<WXNObject> writeMemory = new List<WXNObject>();

        private bool IsPureObject(string text)
        {
            string formatedText = text.Trim();
            return formatedText[0] == '<' && formatedText[^1] == '>';
        }

        private dynamic GetPureObjectData(string text)
        {
            if (text.Contains('<') && text.Contains('>'))
                return DeconstructVector(text);
            else
            {
                if (int.TryParse(text.Trim(), out int dataIntParsed)) 
                    return dataIntParsed;
                else if (bool.TryParse(text.Trim(), out bool dataBoolParsed)) 
                    return dataBoolParsed; 
                else if (float.TryParse(text.Replace('.', ','), out float dataFloatParsed))
                    return dataFloatParsed;
                else
                    return text.Replace('"', ' ').Trim();
            }
        }

        private WXNObject DeconstructObject(string textInFile)
        {
            string[] FileInfoDivision = textInFile.Split((char)WXNSeparators.ObjectValue);
            if (Enum.TryParse<WXNTypes>(FileInfoDivision[0].Split("<")[1].Replace(">", ""), out WXNTypes parsedType))
                return new WXNObject(parsedType, FileInfoDivision[0].Split("<")[0], FileInfoDivision[1]);
            else
                throw new Exception($"Unknown type at {textInFile}");
        }

        private bool CheckExistentID<T>(List<T> objects, T _object) where T : WXNPureObject
        {
            int index = objects.FindIndex(obj => obj.identifier == _object.identifier);
            if (index > 0)
            {
                objects[index].data = _object.data; 
                return true;
            }
            return false;
        }

        private dynamic DeconstructVector(string rawVector)
        {
            string[] vectorValues = rawVector.Replace("<", "").Replace(">", "").Replace('.', ',').Split((char)WXNSeparators.Vector);
            if (vectorValues.Length > 2)
                return new Vector3(float.Parse(vectorValues[0]), float.Parse(vectorValues[1]), float.Parse(vectorValues[2]));
            else
                return new Vector2(float.Parse(vectorValues[0]), float.Parse(vectorValues[1]));
        }

        /// <summary>
        /// Reads a WXN file and converts the data into a WXNFileContent, where you can cath the read data.
        /// </summary>
        /// <param name="FilePath">Path of the file to be read.</param>
        /// <returns></returns>
        public WXNFileContent Read(string FilePath)
        {
            if (Path.GetExtension(FilePath) != ".wxn")
                throw new Exception("The file is not a wxn file");

            var ReturnValue = new List<WXNObject>();
            var ReturnPureValue = new List<WXNPureObject>();

            // Reading file
            string[] FileAsText;
            try { FileAsText = File.ReadAllText(FilePath).Split(new[] { '\n', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries); }
            catch (Exception) { throw new FileNotFoundException(); }

            for (int i = 0; i < FileAsText.Length; i++)
            {
                // Reading normal objects
                if (!IsPureObject(FileAsText[i]))
                {
                    WXNObject @object = DeconstructObject(FileAsText[i]);

                    if (!@object.isArray)
                    {
                        switch (@object.type)
                        {
                            case WXNTypes.Int:
                                @object.data = int.Parse(@object.data);
                                break;
                            case WXNTypes.String:
                                @object.data = @object.data.Replace('"', ' ').Trim();
                                break;
                            case WXNTypes.Bool:
                                @object.data = bool.Parse(@object.data);
                                break;
                            case WXNTypes.Float:
                                @object.data = float.Parse(@object.data.Replace('.', ','));
                                break;
                            case WXNTypes.Vector:
                                @object.data = DeconstructVector(@object.data);
                                break;
                        }
                    }
                    else
                    {
                        string[] RawValueArray = @object.data.Replace("[", "").Replace("]", "").Split((char)WXNSeparators.Array);

                        switch (@object.type)
                        {
                            case WXNTypes.Array_Int:
                                @object.data = Array.ConvertAll(RawValueArray, data => int.Parse(data));
                                break;
                            case WXNTypes.Array_String:
                                for (int a = 0; a < RawValueArray.Length; a++)
                                    RawValueArray[a] = RawValueArray[a].Replace('"', ' ').Trim();

                                @object.data = RawValueArray;
                                break;
                            case WXNTypes.Array_Bool:
                                @object.data = Array.ConvertAll(RawValueArray, data => bool.Parse(data));
                                break;
                            case WXNTypes.Array_Float:
                                @object.data = Array.ConvertAll(RawValueArray, data => float.Parse(data.Replace('.', ',')));
                                break;
                            case WXNTypes.Array_Vector:
                                if (RawValueArray[0].Split((char)WXNSeparators.Vector).Length > 2)
                                    @object.data = Array.ConvertAll<string, Vector3>(RawValueArray, data => DeconstructVector(data));
                                else
                                    @object.data = Array.ConvertAll<string, Vector2>(RawValueArray, data => DeconstructVector(data));
                                break;
                        }
                    }

                    if (!CheckExistentID(ReturnValue, @object))
                        ReturnValue.Add(@object);
                }
                else
                {
                    WXNPureObject _object;
                    
                    string[] data = FileAsText[i][1..^1].Trim().Split((char)WXNSeparators.ObjectValue);
                    if (data[1].Trim()[0] == '[' && data[1].Trim()[^1] == ']')
                    {
                        string[] newData = data[1].Replace("[", "").Replace("]", "").Split((char)WXNSeparators.Array);
                        _object = new WXNPureObject(data[0], Array.ConvertAll(newData, data => GetPureObjectData(data)));
                    }
                    else
                        _object = new WXNPureObject(data[0], GetPureObjectData(data[1]));

                    if (!CheckExistentID(ReturnPureValue, _object))
                        ReturnPureValue.Add(_object);
                }
            }

            return new WXNFileContent(ReturnValue, ReturnPureValue);
        }

        /// <summary>
        /// Clears the pure memory and the normal memory.
        /// </summary>
        public void ClearWriteMemory()
        {
            writeMemory.Clear();
            pureWriteMemory.Clear();
        }

        /// <summary>
        /// Adds to the normal memory the objects and checks if has equals identfiers.
        /// </summary>
        /// <param name="_object">Impure (Explicit) objects.</param>
        public void Write(params WXNObject[] _object)
        {
            for (int i = 0; i < _object.Length; i++)
            {
                if (CheckExistentID(writeMemory, _object[i]))
                    return;
                writeMemory.Add(_object[i]);
            }
        }

        /// <summary>
        /// Adds to the pure memory the objects and checks if has equals identfiers.
        /// </summary>
        /// <param name="_object">Pure (Implicit) objects.</param>
        public void WritePure(params WXNPureObject[] _object)
        {
            for (int i = 0; i < _object.Length; i++)
            {
                if (CheckExistentID(pureWriteMemory, _object[i]))
                    return;
                pureWriteMemory.Add(_object[i]);
            }
        }

        /// <summary>
        ///  Writes a WXN file using the pure memory and the normal memory.
        /// </summary>
        /// <param name="filePath">Path to write\over write the file.</param>
        public void Save(string filePath)
        {
            string text = "";

            // Writing pure objects
            foreach (var item in pureWriteMemory)
            {
                if (item.data.ToString().Contains("[]"))
                {
                    if (item.data.ToString().Contains('<') && item.data.ToString().Contains('>'))
                    {
                        text += $"<{item.identifier}: [";
                        for (int i = 0; i < item.data.Length - 1; i++)
                            text += $"{item.data[i].ToString()}; ";

                        text += $"{item.data[item.data.Length - 1].ToString()}]>\n";
                    }
                    else
                    {
                        text += $"<{item.identifier}: [";
                        for (int i = 0; i < item.data.Length - 1; i++)
                            text += $"{item.data[i]}, ";

                        text += $"{item.data[item.data.Length - 1]}]>\n";
                    }
                }
                else
                {
                    text += $"<{item.identifier}:{item.data}>\n";
                }
            }

            // Writing regular objects
            foreach (var item in writeMemory)
            {
                if (!item.isArray)
                {
                    text += $"{item.identifier}<{item.type}>: {item.data.ToString().Replace('.', ';').Replace(',', '.')}\n";
                }
                else
                {
                    text += $"{item.identifier}<{item.type}>: [ ";
                    switch (item.type)
                    {
                        case WXNTypes.Array_String:
                            for (int i = 0; i < item.data.Length - 1; i++)
                                text += $"\"{item.data[i]}\", ";

                            text += $"\"{item.data[item.data.Length - 1]}\"]\n";
                            break;
                        case WXNTypes.Array_Vector:
                            for (int i = 0; i < item.data.Length - 1; i++)
                                text += $"{item.data[i].ToString().Replace('.', ';').Replace(',', '.')}; ";

                            text += $"{item.data[item.data.Length - 1].ToString().Replace('.', ';').Replace(',', '.')}]\n";
                            break;
                        default:
                            for (int i = 0; i < item.data.Length - 1; i++)
                                text += $"{item.data[i]}, ";

                            text += $"{item.data[item.data.Length - 1]}]\n";
                            break;
                    }
                }
            }

            // Saving
            File.WriteAllText(filePath, text);
        }
    }
}
