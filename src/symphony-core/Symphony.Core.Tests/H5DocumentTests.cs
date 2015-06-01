﻿using System;
using System.Collections.Generic;
using HDF5DotNet;
using NUnit.Framework;

namespace Symphony.Core
{
    class H5DocumentTests
    {
        const string TEST_FILE = "myCSharp.h5";

        private H5Document document;

        [SetUp]
        public void Setup()
        {
            if (System.IO.File.Exists(TEST_FILE))
            {
                System.IO.File.Delete(TEST_FILE);
            }

            document = new H5Document(TEST_FILE);
        }

        [TearDown]
        public void Teardown()
        {
            document.Close();
        }

        [Test]
        public void WriteNote()
        {
            H5DataSetId setId = H5D.open(document.fileId, "notes");

            document.WriteNote(setId, new Note(DateTimeOffset.Now, "one two"));
            document.WriteNote(setId, new Note(DateTimeOffset.Now, "three four"));
            document.WriteNote(setId, new Note(DateTimeOffset.Now, "five six"));

            IEnumerable<Note> notes = document.ReadNotes(setId);

            H5D.close(setId);
        }

        //[Test]
        //public void ReadAttributes()
        //{
        //    H5Document.WriteAttribute(fileId, "boolean", true);
        //    H5Document.WriteAttribute(fileId, "booleanArray", new bool[] { true, false, true });
        //    H5Document.WriteAttribute(fileId, "long", 12);
        //    H5Document.WriteAttribute(fileId, "longArray", new int[] { 1, 2, 3 });
        //    H5Document.WriteAttribute(fileId, "doubleArray", new double[] { 1, 2, 3 });
        //    H5Document.WriteAttribute(fileId, "doubleMatrix", new double[,] { { 1, 2, 3 }, { 4, 5, 6 } });
        //    H5Document.WriteAttribute(fileId, "double", 5.5);
        //    H5Document.WriteAttribute(fileId, "string", "hello");
        //    var d = H5Document.ReadAttributes(fileId);
        //    foreach (var kvp in d)
        //    {
        //        Console.WriteLine(@"{0} = {1}", kvp.Key, kvp.Value);
        //    }
        //}

        //[Test]
        //public void WriteReadBooleanAttribute()
        //{
        //    const bool expected = true;
        //    H5Document.WriteBooleanAttribute(fileId, "attr", expected);
        //    Assert.AreEqual(expected, H5Document.ReadBooleanAttribute(fileId, "attr"));
        //}

        //[Test]
        //public void WriteReadLongAttribute()
        //{
        //    const long expected = 1534;
        //    H5Document.WriteLongAttribute(fileId, "attr", expected);
        //    Assert.AreEqual(expected, H5Document.ReadLongAttribute(fileId, "attr"));
        //}

        //[Test]
        //public void WriteReadDoubleArrayAttribute()
        //{
        //    var expected = new double[] {1.2, 2.3, 4.5, 1.3};
        //    H5Document.WriteDoubleArrayAttribute(fileId, "attr", expected);
        //    Assert.AreEqual(expected, H5Document.ReadDoubleArrayAttribute(fileId, "attr"));
        //}

        //[Test]
        //public void WriteReadStringAttribute()
        //{
        //    const string expected = "Hello World!";
        //    H5Document.WriteStringAttribute(fileId, "attr", expected);
        //    Assert.AreEqual(expected, H5Document.ReadStringAttribute(fileId, "attr"));
        //}

        //[Test]
        //public void OverwriteAttribute()
        //{
        //    H5Document.WriteStringAttribute(fileId, "attr", "one");
        //    H5Document.WriteStringAttribute(fileId, "attr", "two");
        //    Assert.AreEqual("two", H5Document.ReadStringAttribute(fileId, "attr"));
        //}
    }
}
