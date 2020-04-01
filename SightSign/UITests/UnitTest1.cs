using System;
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
        private const string SightSignAppExe = @"SightSign\bin\Release\SightSign.exe";

        protected static WindowsDriver<WindowsElement> session;

        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            if (session == null)
            {
                var parentDirPath = System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory());
                Console.WriteLine(parentDirPath);
                var SightSignAppId = parentDirPath + SightSignAppExe;
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

            Assert.IsFalse(clearButton.Enabled);
            Assert.IsFalse(loadButton.Enabled);
            Assert.IsFalse(saveButton.Enabled);

            editButton.Click();

            Thread.Sleep(TimeSpan.FromSeconds(3));
            
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

            editButton.Click();
            Thread.Sleep(TimeSpan.FromSeconds(3));
            loadButton.Click();
            Thread.Sleep(TimeSpan.FromSeconds(3));

            Assert.IsTrue(loadButton.Text == "Close");
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

            Assert.IsFalse(plusButton.Enabled);
            Assert.IsFalse(minusButton.Enabled);
            Assert.IsFalse(drawAreaButton.Enabled);
            Assert.IsFalse(doneButton.Enabled);

            editButton.Click();
            Thread.Sleep(TimeSpan.FromSeconds(3));
            areaButton.Click();
            Thread.Sleep(TimeSpan.FromSeconds(2));

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

            minusButton.Click();
            Thread.Sleep(TimeSpan.FromSeconds(3));

            Assert.AreNotEqual("8 x 6", areaText.Text);
        }

        [TestMethod]
        public void TestIncreaseAreaButtonClick()
        {

            PointerInputDevice mouseDevice = new PointerInputDevice(PointerKind.Touch);

            var plusButton = session.FindElementByAccessibilityId("IncreaseDrawingAreaButton");
            var areaText = session.FindElementByAccessibilityId("AreaText");
            var doneButton = session.FindElementByAccessibilityId("DoneDrawingAreaButton");


            plusButton.Click();
            Thread.Sleep(TimeSpan.FromSeconds(3));

            Assert.AreNotEqual("6 x 4", areaText.Text);
            doneButton.Click();
            Thread.Sleep(TimeSpan.FromSeconds(3.5));
        }

        [TestMethod]
        public void TestWriteButtonClick()
        {

            PointerInputDevice mouseDevice = new PointerInputDevice(PointerKind.Touch);

            var writeButton = session.FindElementByAccessibilityId("WriteButton");
            var dotButton = session.FindElementByAccessibilityId("Dot");

            writeButton.Click();
            Thread.Sleep(TimeSpan.FromSeconds(4));
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
