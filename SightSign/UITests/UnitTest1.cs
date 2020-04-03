﻿using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace UITests
{
    [TestClass]
    public class SightSignUITests
    {

        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";
        private const string SightSignAppExe = @"\SightSign\bin\Release\SightSign.exe";

        protected static WindowsDriver<WindowsElement> session;

        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            if (session == null)
            {
                var curDirPath = System.IO.Directory.GetCurrentDirectory();
                Console.WriteLine(curDirPath);
                while (System.IO.Directory.GetParent(curDirPath).Name != "SightSign")
                {
                    //Console.WriteLine("Full Path: " + curDirPath);
                    //Console.WriteLine("Cur Dir: " + System.IO.Path.GetDirectoryName(curDirPath));
                    Console.WriteLine("Parent Dir: " + System.IO.Directory.GetParent(curDirPath).Name);
                    
                    curDirPath = System.IO.Directory.GetParent(curDirPath).FullName;
                }
                curDirPath = System.IO.Directory.GetParent(curDirPath).FullName;
                var SightSignAppId = curDirPath + SightSignAppExe;
                Console.WriteLine(SightSignAppId);
                var appiumOptions = new AppiumOptions();
                appiumOptions.AddAdditionalCapability("app", SightSignAppId);
                appiumOptions.AddAdditionalCapability("deviceName", "WindowsPC");
                session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), appiumOptions);

            }
        }

        [TestMethod]
        public void TestEditButtonClick()
        {

            PointerInputDevice mouseDevice = new PointerInputDevice(PointerKind.Touch);

            var editButton = session.FindElementByAccessibilityId("EditButton");
            var clearButton = session.FindElementByAccessibilityId("ClearButton");
            var loadButton = session.FindElementByAccessibilityId("LoadButton");
            var saveButton = session.FindElementByAccessibilityId("SaveButton");

            Assert.AreNotEqual(editButton,null);
            Assert.AreNotEqual(clearButton,null);
            Assert.AreNotEqual(loadButton,null);
            Assert.AreNotEqual(saveButton,null);

            Assert.IsFalse(clearButton.Enabled);
            Assert.IsFalse(loadButton.Enabled);
            Assert.IsFalse(saveButton.Enabled);

            editButton.Click();

            Thread.Sleep(10000);
            
            Assert.IsTrue(clearButton.Enabled);
            Assert.IsTrue(loadButton.Enabled);
            Assert.IsTrue(saveButton.Enabled);
        }

        // Load button test. Couldn't access sigbank, testing load button text instead.
        [TestMethod]
        public void TestLoadButtonClick()
        {

            PointerInputDevice mouseDevice = new PointerInputDevice(PointerKind.Touch);

            var editButton = session.FindElementByAccessibilityId("EditButton");
            var loadButton = session.FindElementByAccessibilityId("LoadButton");

            Assert.AreNotEqual(editButton,null);
            Assert.AreNotEqual(loadButton,null);

            editButton.Click();
            Thread.Sleep(10000);
            loadButton.Click();
            Thread.Sleep(10000);

            editButton = session.FindElementByAccessibilityId("EditButton");
            loadButton = session.FindElementByAccessibilityId("LoadButton");

            Console.WriteLine("Type={0}",loadButton.GetType());
            foreach(System.ComponentModel.PropertyDescriptor descriptor in System.ComponentModel.TypeDescriptor.GetProperties(loadButton))
            {
                string name=descriptor.Name;
                object value=descriptor.GetValue(loadButton);
                Console.WriteLine("{0}={1}",name,value);
            }

            Assert.IsEqual("Close",loadButton.Text);
        }

        // Save button test - nothing really to test with the UI

        // Clear button test - can't access the ink canvas properties through this method

        //area make sure the following buttons are displayed

        [TestMethod]
        public void TestAreaButtonClick()
        {

            PointerInputDevice mouseDevice = new PointerInputDevice(PointerKind.Touch);

            var editButton = session.FindElementByAccessibilityId("EditButton");
            var areaButton = session.FindElementByAccessibilityId("AreaButton");
            var plusButton = session.FindElementByAccessibilityId("IncreaseDrawingAreaButton");
            var minusButton = session.FindElementByAccessibilityId("DecreaseDrawingAreaButton");
            var drawAreaButton = session.FindElementByAccessibilityId("DrawAreaButton");
            var doneButton = session.FindElementByAccessibilityId("DoneDrawingAreaButton");

            Assert.AreNotEqual(editButton,null);
            Assert.AreNotEqual(areaButton,null);
            Assert.AreNotEqual(plusButton,null);
            Assert.AreNotEqual(minusButton,null);
            Assert.AreNotEqual(drawAreaButton,null);
            Assert.AreNotEqual(doneButton,null);

            Assert.IsFalse(plusButton.Enabled);
            Assert.IsFalse(minusButton.Enabled);
            Assert.IsFalse(drawAreaButton.Enabled);
            Assert.IsFalse(doneButton.Enabled);

            editButton.Click();
            Thread.Sleep(10000);
            areaButton.Click();
            Thread.Sleep(10000);

            Assert.IsTrue(plusButton.Enabled);
            Assert.IsTrue(minusButton.Enabled);
            Assert.IsTrue(drawAreaButton.Enabled);
            Assert.IsTrue(doneButton.Enabled);
        }

        [TestMethod]
        public void TestDecreaseAreaButtonClick()
        {

            PointerInputDevice mouseDevice = new PointerInputDevice(PointerKind.Touch);

            var minusButton = session.FindElementByAccessibilityId("DecreaseDrawingAreaButton");
            var areaText = session.FindElementByAccessibilityId("AreaText");

            Assert.AreNotEqual(minusButton,null);

            Assert.AreEqual("8'' X 6''", areaText.Text);    

            minusButton.Click();
            Thread.Sleep(10000);

            Assert.AreEqual("6'' X 4''", areaText.Text);
        }

        [TestMethod]
        public void TestIncreaseAreaButtonClick()
        {

            PointerInputDevice mouseDevice = new PointerInputDevice(PointerKind.Touch);

            var plusButton = session.FindElementByAccessibilityId("IncreaseDrawingAreaButton");
            var areaText = session.FindElementByAccessibilityId("AreaText");

            Assert.AreNotEqual(plusButton,null);

            Assert.AreEqual("6'' X 4''", areaText.Text);

            plusButton.Click();
            Thread.Sleep(10000);

            Assert.AreEqual("8'' X 6''", areaText.Text);
        }

        [TestMethod]
        public void TestWriteButtonClick()
        {

            PointerInputDevice mouseDevice = new PointerInputDevice(PointerKind.Touch);

            var writeButton = session.FindElementByAccessibilityId("WriteButton");
            var dotButton = session.FindElementByAccessibilityId("Dot");
            var doneButton = session.FindElementByAccessibilityId("DoneDrawingAreaButton");

            Assert.AreNotEqual(writeButton,null);
            Assert.AreNotEqual(dotButton,null);
            Assert.AreNotEqual(doneButton,null);

            doneButton.Click();
            Thread.Sleep(20000);

            foreach(System.ComponentModel.PropertyDescriptor descriptor in System.ComponentModel.TypeDescriptor.GetProperties(writeButton))
            {
                string name=descriptor.Name;
                object value=descriptor.GetValue(writeButton);
                Console.WriteLine("{0}={1}",name,value);
            }

            writeButton.Click();
            Thread.Sleep(10000);
            Assert.IsTrue(dotButton.Enabled);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            // Close the application and delete the session
            if (session != null)
            {
                session.Close();
                session.Quit();
                session = null;
            }
        }
    }
}
