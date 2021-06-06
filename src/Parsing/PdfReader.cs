﻿// Copyright (c) PdfToSvg.NET contributors.
// https://github.com/dmester/pdftosvg.net
// Licensed under the MIT License.

using PdfToSvg.Common;
using PdfToSvg.DocumentModel;
using PdfToSvg.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PdfToSvg.Parsing
{
    internal static class PdfReader
    {
        public static PdfDocument Read(InputFile file)
        {
            using (var reader = file.CreateExclusiveReader())
            {
                var parser = new DocumentParser(file, reader);

                parser.ReadFileHeader();

                var startxref = parser.ReadStartXRef();
                var xrefTable = parser.ReadXRefTables(startxref);

                if (xrefTable.Trailer != null && 
                    xrefTable.Trailer.ContainsKey(Names.Encrypt))
                {
                    throw Exceptions.EncryptedPdf();
                }

                var objects = parser.ReadAllObjects(xrefTable);

                InlineReferences(objects);
                InlineReferences(objects, xrefTable.Trailer);

                return new PdfDocument(file, xrefTable.Trailer, objects);
            }
        }

        public static async Task<PdfDocument> ReadAsync(InputFile file)
        {
            using (var reader = await file.CreateExclusiveReaderAsync())
            {
                var parser = new DocumentParser(file, reader);

                await parser.ReadFileHeaderAsync();

                var startxref = await parser.ReadStartXRefAsync();
                var xrefTable = parser.ReadXRefTables(startxref); // TODO make async

                if (xrefTable.Trailer.ContainsKey(Names.Encrypt))
                {
                    throw Exceptions.EncryptedPdf();
                }

                var objects = parser.ReadAllObjects(xrefTable); // TODO make async

                InlineReferences(objects);
                InlineReferences(objects, xrefTable.Trailer);

                return new PdfDocument(file, xrefTable.Trailer, objects);
            }
        }

        public static IList<PdfDictionary> GetPageDictionaries(PdfDictionary root)
        {
            var pages = new List<PdfDictionary>();
            var pagesStack = new Stack<IEnumerator>();

            if (root.TryGetDictionary(Names.Pages, out var rootPagesDict))
            {
                if (rootPagesDict.TryGetArray(Names.Kids, out var kids))
                {
                    pagesStack.Push(kids.GetEnumerator());
                }
            }

            while (pagesStack.Count > 0)
            {
                var enumerator = pagesStack.Peek();
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current;

                    if (value is PdfDictionary dict && dict.TryGetName(Names.Type, out var name))
                    {
                        if (name == Names.Pages)
                        {
                            if (dict.TryGetArray(Names.Kids, out var kids))
                            {
                                pagesStack.Push(kids.GetEnumerator());
                            }
                        }
                        else if (name == Names.Page)
                        {
                            pages.Add(dict);
                        }
                    }
                }
                else
                {
                    (enumerator as IDisposable)?.Dispose();
                    pagesStack.Pop();
                }
            }

            return pages;
        }

        private static void InlineReferences(Dictionary<PdfObjectId, object?> objects)
        {
            foreach (var pair in objects)
            {
                InlineReferences(objects, pair.Value);
            }
        }

        private static void InlineReferences(Dictionary<PdfObjectId, object?> objects, object? value)
        {
            if (value is PdfDictionary dict)
            {
                var refs = new List<KeyValuePair<PdfName, PdfObjectId>>();

                foreach (var pair in dict)
                {
                    if (pair.Value is PdfRef reference)
                    {
                        refs.Add(new KeyValuePair<PdfName, PdfObjectId>(pair.Key, reference.Id));
                    }
                    else
                    {
                        InlineReferences(objects, pair.Value);
                    }
                }

                foreach (var reference in refs)
                {
                    if (objects.TryGetValue(reference.Value, out var referencedValue))
                    {
                        dict[reference.Key] = referencedValue;
                    }
                    else
                    {
                        Log.WriteLine($"Reference to missing object {reference.Value}.");
                    }
                }
            }
            else if (value is object?[] arr)
            {
                for (var i = 0; i < arr.Length; i++)
                {
                    if (arr[i] is PdfRef reference)
                    {
                        if (objects.TryGetValue(reference.Id, out var referencedValue))
                        {
                            arr[i] = referencedValue;
                        }
                        else
                        {
                            Log.WriteLine($"Reference to missing object ({reference.Id}).");
                        }
                    }
                    else
                    {
                        InlineReferences(objects, arr[i]);
                    }
                }
            }
        }
    }
}
