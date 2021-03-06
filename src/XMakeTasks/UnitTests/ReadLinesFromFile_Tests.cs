﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class ReadLinesFromFile_Tests
    {
        /// <summary>
        /// Write one line, read one line.
        /// </summary>
        [TestMethod]
        public void Basic()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                WriteLinesToFile a = new WriteLinesToFile();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("Line1") };
                Assert.IsTrue(a.Execute());

                // Read the line from the file.
                ReadLinesFromFile r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.IsTrue(r.Execute());

                Assert.AreEqual(1, r.Lines.Length);
                Assert.AreEqual("Line1", r.Lines[0].ItemSpec);

                // Write two more lines to the file.
                a.Lines = new ITaskItem[] { new TaskItem("Line2"), new TaskItem("Line3") };
                Assert.IsTrue(a.Execute());

                // Read all of the lines and verify them.
                Assert.IsTrue(r.Execute());
                Assert.AreEqual(3, r.Lines.Length);
                Assert.AreEqual("Line1", r.Lines[0].ItemSpec);
                Assert.AreEqual("Line2", r.Lines[1].ItemSpec);
                Assert.AreEqual("Line3", r.Lines[2].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Write one line, read one line, where the line contains MSBuild-escapable characters.  
        /// The file should contain the *unescaped* lines, but no escaping information should be 
        /// lost when read. 
        /// </summary>
        [TestMethod]
        public void Escaping()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                WriteLinesToFile a = new WriteLinesToFile();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("Line1_%253b_") };
                Assert.IsTrue(a.Execute());

                // Read the line from the file.
                ReadLinesFromFile r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.IsTrue(r.Execute());

                Assert.AreEqual(1, r.Lines.Length);
                Assert.AreEqual("Line1_%3b_", r.Lines[0].ItemSpec);

                // Write two more lines to the file.
                a.Lines = new ITaskItem[] { new TaskItem("Line2"), new TaskItem("Line3") };
                Assert.IsTrue(a.Execute());

                // Read all of the lines and verify them.
                Assert.IsTrue(r.Execute());
                Assert.AreEqual(3, r.Lines.Length);
                Assert.AreEqual("Line1_%3b_", r.Lines[0].ItemSpec);
                Assert.AreEqual("Line2", r.Lines[1].ItemSpec);
                Assert.AreEqual("Line3", r.Lines[2].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Write a line that contains an ANSI character that is not ASCII.
        /// </summary>
        [TestMethod]
        public void ANSINonASCII()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                WriteLinesToFile a = new WriteLinesToFile();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("My special character is \u00C3") };
                Assert.IsTrue(a.Execute());

                // Read the line from the file.
                ReadLinesFromFile r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.IsTrue(r.Execute());

                Assert.AreEqual(1, r.Lines.Length);
                Assert.AreEqual("My special character is \u00C3", r.Lines[0].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Reading lines from an missing file should result in the empty list.
        /// </summary>
        [TestMethod]
        public void ReadMissing()
        {
            string file = FileUtilities.GetTemporaryFile();
            File.Delete(file);

            // Read the line from the file.
            ReadLinesFromFile r = new ReadLinesFromFile();
            r.File = new TaskItem(file);
            Assert.IsTrue(r.Execute());

            Assert.AreEqual(0, r.Lines.Length);
        }

        /// <summary>
        /// Reading blank lines from a file should be ignored.
        /// </summary>
        [TestMethod]
        public void IgnoreBlankLines()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                WriteLinesToFile a = new WriteLinesToFile();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[]
                {
                    new TaskItem("Line1"),
                    new TaskItem("  "),
                    new TaskItem("Line2"),
                    new TaskItem(""),
                    new TaskItem("Line3"),
                    new TaskItem("\0\0\0\0\0\0\0\0\0")
                };
                Assert.IsTrue(a.Execute());

                // Read the line from the file.
                ReadLinesFromFile r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.IsTrue(r.Execute());

                Assert.AreEqual(3, r.Lines.Length);
                Assert.AreEqual("Line1", r.Lines[0].ItemSpec);
                Assert.AreEqual("Line2", r.Lines[1].ItemSpec);
                Assert.AreEqual("Line3", r.Lines[2].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Reading lines from a file that you have no access to.
        /// </summary>
        [TestMethod]
        public void ReadNoAccess()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Start with a missing file.
                File.Delete(file);

                // Append one line to the file.
                WriteLinesToFile a = new WriteLinesToFile();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("This is a new line") };
                Assert.IsTrue(a.Execute());

                // Remove all File access to the file to current user
                FileSecurity fSecurity = File.GetAccessControl(file);
                string userAccount = string.Format(@"{0}\{1}", System.Environment.UserDomainName, System.Environment.UserName);
                fSecurity.AddAccessRule(new FileSystemAccessRule(userAccount, FileSystemRights.ReadData, AccessControlType.Deny));
                File.SetAccessControl(file, fSecurity);

                // Attempt to Read lines from the file.
                ReadLinesFromFile r = new ReadLinesFromFile();
                MockEngine mEngine = new MockEngine();
                r.BuildEngine = mEngine;
                r.File = new TaskItem(file);
                Assert.IsFalse(r.Execute());
            }
            finally
            {
                FileSecurity fSecurity = File.GetAccessControl(file);
                string userAccount = string.Format(@"{0}\{1}", System.Environment.UserDomainName, System.Environment.UserName);
                fSecurity.AddAccessRule(new FileSystemAccessRule(userAccount, FileSystemRights.ReadData, AccessControlType.Allow));
                File.SetAccessControl(file, fSecurity);

                // Delete file
                File.Delete(file);
            }
        }

        /// <summary>
        /// Invalid encoding
        /// </summary>
        [TestMethod]
        public void InvalidEncoding()
        {
            WriteLinesToFile a = new WriteLinesToFile();
            a.BuildEngine = new MockEngine();
            a.Encoding = "||invalid||";
            a.File = new TaskItem("c:\\" + Guid.NewGuid().ToString());
            a.Lines = new TaskItem[] { new TaskItem("x") };

            Assert.AreEqual(false, a.Execute());
            ((MockEngine)a.BuildEngine).AssertLogContains("MSB3098");
            Assert.AreEqual(false, File.Exists(a.File.ItemSpec));
        }

        /// <summary>
        /// Reading blank lines from a file should be ignored.
        /// </summary>
        [TestMethod]
        public void Encoding()
        {
            string file = FileUtilities.GetTemporaryFile();
            try
            {
                // Write default encoding: UTF8
                WriteLinesToFile a = new WriteLinesToFile();
                a.BuildEngine = new MockEngine();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("\uBDEA") };
                Assert.IsTrue(a.Execute());

                ReadLinesFromFile r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.IsTrue(r.Execute());

                Assert.IsTrue("\uBDEA" == r.Lines[0].ItemSpec);

                File.Delete(file);

                // Write ANSI .. that won't work! 
                a = new WriteLinesToFile();
                a.BuildEngine = new MockEngine();
                a.File = new TaskItem(file);
                a.Lines = new ITaskItem[] { new TaskItem("\uBDEA") };
                a.Encoding = "ASCII";
                Assert.IsTrue(a.Execute());

                // Read the line from the file.
                r = new ReadLinesFromFile();
                r.File = new TaskItem(file);
                Assert.IsTrue(r.Execute());

                Assert.IsTrue("\uBDEA" != r.Lines[0].ItemSpec);
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
