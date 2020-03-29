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
        private const string SightSignAppId = @"C:\Users\brcam\Documents\SightSign\SightSign\SightSign\bin\Debug\SightSign.exe";

        protected static WindowsDriver<WindowsElement> session;

        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            if (session == null)
            {
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
    }
}
