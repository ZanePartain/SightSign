using System;
using System.Collections.Generic;
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
            // The edit button gets pressed when running the test, but the last assert
            // doesn't work although it should be working.
            Assert.IsFalse(session.FindElementByAccessibilityId("ClearButton").Enabled);
            session.FindElementByAccessibilityId("EditButton").Click();
            Assert.IsTrue(session.FindElementByAccessibilityId("ClearButton").Enabled);
        }
    }
}
