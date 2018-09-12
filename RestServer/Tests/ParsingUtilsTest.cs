using System;
using Models;
using RestServer.Util;
using Xunit;

namespace RestServer.Tests
{
    public sealed class ParsingUtilsTest
    {
        [Fact]
        public void TestUnprefixedPhone()
        {
            var phoneObject = ParsingUtils.ParsePhoneNumber("11 99751-3126");
            Assert.Equal(PhoneCountry.Brazil, phoneObject.CountryCode);
            Assert.Equal(PhoneRegion.SpCapital, phoneObject.StateCode);
            Assert.Equal(Convert.ToString(997513126L), Convert.ToString(phoneObject.Number));
        }

        [Fact]
        public void TestPrefixedPhone()
        {
            var phoneObject = ParsingUtils.ParsePhoneNumber("+55 11 99751-3126");
            Assert.Equal(PhoneCountry.Brazil, phoneObject.CountryCode);
            Assert.Equal(PhoneRegion.SpCapital, phoneObject.StateCode);
            Assert.Equal(Convert.ToString(997513126L), Convert.ToString(phoneObject.Number));
        }

        [Fact]
        public void TestSpacedPhone()
        {
            var phoneObject = ParsingUtils.ParsePhoneNumber("+55 11 9 9751-3126");
            Assert.Equal(PhoneCountry.Brazil, phoneObject.CountryCode);
            Assert.Equal(PhoneRegion.SpCapital, phoneObject.StateCode);
            Assert.Equal(Convert.ToString(997513126L), Convert.ToString(phoneObject.Number));
        }

        [Fact]
        public void TestUnspacedPhone()
        {
            var phoneObject = ParsingUtils.ParsePhoneNumber("+551199751-3126");
            Assert.Equal(PhoneCountry.Brazil, phoneObject.CountryCode);
            Assert.Equal(PhoneRegion.SpCapital, phoneObject.StateCode);
            Assert.Equal(Convert.ToString(997513126L), Convert.ToString(phoneObject.Number));
        }

        [Fact]
        public void TestUnplussedPhone()
        {
            var phoneObject = ParsingUtils.ParsePhoneNumber("551199751-3126");
            Assert.Equal(PhoneCountry.Brazil, phoneObject.CountryCode);
            Assert.Equal(PhoneRegion.SpCapital, phoneObject.StateCode);
            Assert.Equal(Convert.ToString(997513126L), Convert.ToString(phoneObject.Number));
        }

        [Fact]
        public void TestNoSeparatorPhone()
        {
            var phoneObject = ParsingUtils.ParsePhoneNumber("+5511997513126");
            Assert.Equal(PhoneCountry.Brazil, phoneObject.CountryCode);
            Assert.Equal(PhoneRegion.SpCapital, phoneObject.StateCode);
            Assert.Equal(Convert.ToString(997513126L), Convert.ToString(phoneObject.Number));
        }

        [Fact]
        public void TestParenthesisPhone()
        {
            var phoneObject = ParsingUtils.ParsePhoneNumber("+55 (11) 997513126");
            Assert.Equal(PhoneCountry.Brazil, phoneObject.CountryCode);
            Assert.Equal(PhoneRegion.SpCapital, phoneObject.StateCode);
            Assert.Equal(Convert.ToString(997513126L), Convert.ToString(phoneObject.Number));
        }
    }
}
