﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.OData.Builder;
using System.Web.OData.Builder.TestModels;
using System.Web.OData.Extensions;
using Microsoft.OData.Edm;
using Microsoft.TestCommon;
using Newtonsoft.Json.Linq;

namespace System.Web.OData
{
    public class DateTimeTest
    {
        private readonly TimeZoneInfo _utcTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("UTC");
        private readonly TimeZoneInfo _pacificStandard = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private readonly TimeZoneInfo _chinaStandard = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        private readonly string _bastUri = "http://localhost/odata/DateTimeModels";

        [Fact]
        public void MetadataDocument_IncludesDateTimeProperties()
        {
            // Arrange
            const string Uri = "http://localhost/odata/$metadata";
            const string Expected =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<edmx:Edmx Version=\"4.0\" xmlns:edmx=\"http://docs.oasis-open.org/odata/ns/edmx\">\r\n" +
                "  <edmx:DataServices>\r\n" +
                "    <Schema Namespace=\"System.Web.OData.Builder.TestModels\" xmlns=\"http://docs.oasis-open.org/odata/ns/edm\">\r\n" +
                "      <EntityType Name=\"DateTimeModel\">\r\n" +
                "        <Key>\r\n" +
                "          <PropertyRef Name=\"Id\" />\r\n" +
                "        </Key>\r\n" +
                "        <Property Name=\"Id\" Type=\"Edm.Int32\" Nullable=\"false\" />\r\n" +
                "        <Property Name=\"BirthdayA\" Type=\"Edm.DateTimeOffset\" Nullable=\"false\" />\r\n" +
                "        <Property Name=\"BirthdayB\" Type=\"Edm.DateTimeOffset\" />\r\n" +
                "        <Property Name=\"BirthdayC\" Type=\"Collection(Edm.DateTimeOffset)\" Nullable=\"false\" />\r\n" +
                "        <Property Name=\"BirthdayD\" Type=\"Collection(Edm.DateTimeOffset)\" />\r\n" +
                "      </EntityType>\r\n" +
                "    </Schema>\r\n" +
                "    <Schema Namespace=\"Default\" xmlns=\"http://docs.oasis-open.org/odata/ns/edm\">\r\n" +
                "      <Function Name=\"CalcBirthday\" IsBound=\"true\">\r\n" +
                "        <Parameter Name=\"bindingParameter\" Type=\"System.Web.OData.Builder.TestModels.DateTimeModel\" />\r\n" +
                "        <Parameter Name=\"dto\" Type=\"Edm.DateTimeOffset\" Nullable=\"false\" />\r\n" +
                "        <ReturnType Type=\"Edm.DateTimeOffset\" Nullable=\"false\" />\r\n" +
                "      </Function>\r\n" +
                "      <EntityContainer Name=\"Container\">\r\n" +
                "        <EntitySet Name=\"DateTimeModels\" EntityType=\"System.Web.OData.Builder.TestModels.DateTimeModel\" />\r\n" +
                "      </EntityContainer>\r\n" +
                "    </Schema>\r\n" +
                "  </edmx:DataServices>\r\n" +
                "</edmx:Edmx>";
            HttpClient client = GetClient(timeZoneInfo: null);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Uri);

            // Act
            HttpResponseMessage response = client.SendAsync(request).Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(Expected, response.Content.ReadAsStringAsync().Result);
        }

        [Fact]
        public void CanQueryEntitySet_WithDateTimeProperties()
        {
            // Arrange
            DateTimeOffset expect = new DateTimeOffset(new DateTime(2015, 12, 31, 20, 12, 30, DateTimeKind.Utc));
            const string Uri = "http://localhost/odata/DateTimeModels";
            HttpClient client = GetClient(timeZoneInfo: null);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Uri);

            // Act
            HttpResponseMessage response = client.SendAsync(request).Result;
            var result = JObject.Parse(response.Content.ReadAsStringAsync().Result);

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("http://localhost/odata/$metadata#DateTimeModels", result["@odata.context"]);

            var values = result["value"];
            Assert.Equal(5, values.Count());

            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(expect, (DateTimeOffset)values[i]["BirthdayA"]);
                expect = expect.AddYears(1);
            }
        }

        [Fact]
        public void CanQuerySingleEntity_WithDateTimeProperties_CustomTimeZoneInfo()
        {
            // Arrange
            const string Expected = "  ],\"BirthdayD@odata.type\":\"#Collection(DateTimeOffset)\",\"BirthdayD\":[\r\n" +
               "    \"2018-12-31T12:12:30-08:00\",null,\"2015-04-30T12:12:30-08:00\"\r\n";

            const string Uri = "http://localhost/odata/DateTimeModels(2)";
            HttpClient client = GetClient(_pacificStandard);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Uri);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=full"));

            // Act
            HttpResponseMessage response = client.SendAsync(request).Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Contains(Expected, response.Content.ReadAsStringAsync().Result);
        }

        [Fact]
        public void CanSelect_OnDateTimeProperty()
        {
            // Arrange
            const string Expected = "{\r\n" +
                "  \"@odata.context\":\"http://localhost/odata/$metadata#DateTimeModels(BirthdayB,BirthdayC)/$entity\"," +
                "\"BirthdayB\":\"2015-04-01T04:12:30+08:00\",\"BirthdayC\":[\r\n" +
                "    \"2018-01-01T04:12:30+08:00\",\"2015-04-01T04:12:30+08:00\",\"2015-01-04T04:12:30+08:00\"\r\n" +
                "  ]\r\n" +
                "}";
            const string Uri = @"http://localhost/odata/DateTimeModels(3)?$select=BirthdayB,BirthdayC";
            HttpClient client = GetClient(_chinaStandard);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Uri);

            // Act
            HttpResponseMessage response = client.SendAsync(request).Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(Expected, response.Content.ReadAsStringAsync().Result);
        }

        [Theory]
        [InlineData("UTC")] // +0:00
        [InlineData("Pacific Standard Time")] // -8:00
        // [InlineData("China Standard Time")] // +8:00
        public void CanFilter_OnDateTimeProperty_WithDifferentTimeZoneInfo(string timeZoneId)
        {
            // Arrange
            const string uri1 = "http://localhost/odata/DateTimeModels?$filter=BirthdayB lt cast(2015-04-01T04:11:31%2B08:00,Edm.DateTimeOffset)";
            const string uri2 = "http://localhost/odata/DateTimeModels?$filter=cast(2015-04-01T04:11:31%2B08:00,Edm.DateTimeOffset) ge BirthdayB";

            var client1 = GetClient(timeZoneInfo: null);
            var client2 = GetClient(timeZoneInfo: TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

            // Act
            var response1 = client1.GetAsync(uri1).Result;
            var response2 = client2.GetAsync(uri2).Result;

            // Assert
            Assert.True(response1.IsSuccessStatusCode);
            Assert.True(response2.IsSuccessStatusCode);

            string payload1 = response1.Content.ReadAsStringAsync().Result;
            string payload2 = response2.Content.ReadAsStringAsync().Result;
            Assert.Equal(payload1, payload2);

            var result = JObject.Parse(payload1);
            Assert.Equal(2, result["value"].Count());
            Assert.Equal(DateTimeOffset.Parse("2016-01-01T04:12:30+08:00"), result["value"][0]["BirthdayA"]);
            Assert.Equal(DateTimeOffset.Parse("2017-01-01T04:12:30+08:00"), result["value"][1]["BirthdayA"]);
        }

        [Theory]
        [InlineData("?$filter=BirthdayA lt cast(2015-04-01T04:11:31%2B08:00,Edm.DateTimeOffset)")]
        [InlineData("?$filter=cast(2015-04-01T04:11:31%2B08:00,Edm.DateTimeOffset) ge BirthdayA")]
        [InlineData("?$filter=2015-04-01T04:11:31Z ge BirthdayA")]
        [InlineData("?$filter=BirthdayA lt 2015-04-01T04:11:31Z")]
        [InlineData("?$filter=BirthdayB lt 2015-04-01T04:11:31Z")]
        [InlineData("?$filter=2015-04-01T04:11:31Z ge BirthdayB")]
        public void CanFilter_OnDateTimeProperty_WithDateTimeAndDateTimeOffset(string uri)
        {
            // Arrange
            var client = GetClient(timeZoneInfo: null);

            // Act
            var response = client.GetAsync(_bastUri + uri).Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);
        }

        [Theory]
        [InlineData("UTC", 5)] // +0:00
        [InlineData("Pacific Standard Time", 5)] // -8:00
        [InlineData("China Standard Time", 5)] // +8:00
        public void CanFilter_OnDateTimePropertyWithBuiltInFunction(string timeZoneId, int expectId)
        {
            // Arrange
            const string Uri = "http://localhost/odata/DateTimeModels?$filter=year(BirthdayA) eq 2019";
            HttpClient client = GetClient(timeZoneInfo: TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
            var request = new HttpRequestMessage(HttpMethod.Get, Uri);

            // Act
            var response = client.SendAsync(request).Result;

            Assert.True(response.IsSuccessStatusCode);
            var result = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            Console.WriteLine(response.Content.ReadAsStringAsync().Result);
            Assert.Single(result["value"]);
            Assert.Equal(expectId, result["value"][0]["Id"]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("UTC")] // +0:00
        [InlineData("Pacific Standard Time")] // -8:00
        [InlineData("China Standard Time")] // +8:00
        public void CanOrderBy_OnDateTimeProperty(string timeZoneId)
        {
            // Arrange
            TimeZoneInfo tzi = String.IsNullOrEmpty(timeZoneId) ? null : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            const string Uri = "http://localhost/odata/DateTimeModels?$orderby=BirthdayA desc";
            HttpClient client = GetClient(timeZoneInfo: tzi);
            var request = new HttpRequestMessage(HttpMethod.Get, Uri);

            // Act
            var response = client.SendAsync(request).Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);

            var result = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            Assert.Equal(5, result["value"].Count());

            Assert.Equal(DateTimeOffset.Parse("2020-01-01T04:12:30+08:00"), result["value"][0]["BirthdayA"]);
            Assert.Equal(DateTimeOffset.Parse("2019-01-01T04:12:30+08:00"), result["value"][1]["BirthdayA"]);
            Assert.Equal(DateTimeOffset.Parse("2018-01-01T04:12:30+08:00"), result["value"][2]["BirthdayA"]);
            Assert.Equal(DateTimeOffset.Parse("2017-01-01T04:12:30+08:00"), result["value"][3]["BirthdayA"]);
            Assert.Equal(DateTimeOffset.Parse("2016-01-01T04:12:30+08:00"), result["value"][4]["BirthdayA"]);
        }

        [Fact]
        public void PostEntity_WithDateTimeProperties_OnCustomTimeZone()
        {
            // Arrange
            const string Payload = "{" +
                "\"@odata.context\":\"http://localhost/odata/$metadata#DateTimeModels/$entity\"," +
                "\"Id\":99," +
                "\"BirthdayA\":\"2099-01-01T08:01:02+08:00\"," +
                "\"BirthdayB\":\"2099-02-02T08:01:02+08:00\"," +
                "\"BirthdayC\":[" +
                    "\"2099-03-03T09:03:03+08:00\"" +
                "],\"BirthdayD\":[" +
                    "\"2099-03-03T10:04:04+08:00\",null,\"2099-04-04T11:04:06+08:00\"" +
                "]" +
                "}";
            const string Uri = "http://localhost/odata/DateTimeModels";
            HttpClient client = GetClient(_pacificStandard);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Uri);

            request.Content = new StringContent(Payload);
            request.Content.Headers.ContentType = MediaTypeWithQualityHeaderValue.Parse("application/json");

            // Act
            HttpResponseMessage response = client.SendAsync(request).Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(new Uri("http://localhost/odata/DateTimeModels(99)"), response.Headers.Location);
        }

        [Fact]
        public void PutEntity_WithDateTimeProperties()
        {
            // Arrange
            const string Payload = "{" +
                "\"BirthdayA\":\"2099-01-01T01:02:03+08:00\"," + // +8 Zone
                "\"BirthdayB\":null" +
                "}";

            const string Uri = "http://localhost/odata/DateTimeModels(3)";
            HttpClient client = GetClient(_utcTimeZoneInfo);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, Uri);

            request.Content = new StringContent(Payload);
            request.Content.Headers.ContentType = MediaTypeWithQualityHeaderValue.Parse("application/json");

            // Act
            HttpResponseMessage response = client.SendAsync(request).Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public void CanQuerySingleDateTimeProperty()
        {
            // Arrange
            const string Expected =
                "{\r\n" +
                "  \"@odata.context\":\"http://localhost/odata/$metadata#Collection(Edm.DateTimeOffset)\",\"value\":[\r\n" +
                "    \"2019-01-01T04:12:30+08:00\",null,\"2015-05-01T04:12:30+08:00\"\r\n" +
                "  ]\r\n" +
                "}";

            const string Uri = "http://localhost/odata/DateTimeModels(2)/BirthdayD";
            HttpClient client = GetClient(_chinaStandard);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Uri);

            // Act
            HttpResponseMessage response = client.SendAsync(request).Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(Expected, response.Content.ReadAsStringAsync().Result);
        }

        [Fact]
        public void FunctionsWorksOnDateTime()
        {
            // Arrange
            const string Expected =
                "{\r\n" +
                "  \"@odata.context\":\"http://localhost/odata/$metadata#Edm.DateTimeOffset\"," +
                "\"value\":\"1978-11-14T16:12:00-08:00\"\r\n" +
                "}";
            const string Uri =
                "http://localhost/odata/DateTimeModels(2)/Default.CalcBirthday(dto=2012-12-22T01:02:03Z)";
            HttpClient client = GetClient(_pacificStandard);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Uri);

            // Act
            HttpResponseMessage response = client.SendAsync(request).Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(Expected, response.Content.ReadAsStringAsync().Result);
        }

        private static HttpClient GetClient(TimeZoneInfo timeZoneInfo)
        {
            HttpConfiguration config =
                new[] { typeof(MetadataController), typeof(DateTimeModelsController) }.GetHttpConfiguration();
            if (timeZoneInfo != null)
            {
                config.SetTimeZoneInfo(timeZoneInfo);
            }
            else
            {
                config.SetTimeZoneInfo(TimeZoneInfo.Local);
            }

            config.MapODataServiceRoute("odata", "odata", GetEdmModel());
            return new HttpClient(new HttpServer(config));
        }

        private static IEdmModel GetEdmModel()
        {
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
            builder.EntitySet<DateTimeModel>("DateTimeModels");

            FunctionConfiguration function = builder.EntityType<DateTimeModel>().Function("CalcBirthday");
            function.Returns<DateTime>().Parameter<DateTime>("dto");
            return builder.GetEdmModel();
        }
    }

    public class DateTimeModelsController : ODataController
    {
        private DateTimeModelContext db = new DateTimeModelContext();

        [EnableQuery]
        public IHttpActionResult Get()
        {
            return Ok(db.DateTimes);
        }

        [EnableQuery]
        public IHttpActionResult Get(int key)
        {
            DateTimeModel dtm = db.DateTimes.FirstOrDefault(e => e.Id == key);
            if (dtm == null)
            {
                return NotFound();
            }

            return Ok(dtm);
        }

        public IHttpActionResult Post([FromBody]DateTimeModel dt)
        {
            Assert.NotNull(dt);

            Assert.Equal(99, dt.Id);
            Assert.Equal(new DateTime(2098, 12, 31, 16, 1, 2, DateTimeKind.Unspecified), dt.BirthdayA);
            Assert.Equal(new DateTime(2099, 2, 1, 16, 1, 2), dt.BirthdayB);
            Assert.Equal(1, dt.BirthdayC.Count);
            Assert.Equal(3, dt.BirthdayD.Count);

            return Created(dt);
        }

        public IHttpActionResult Put(int key, Delta<DateTimeModel> dt)
        {
            Assert.Equal(new[] { "BirthdayA", "BirthdayB" }, dt.GetChangedPropertyNames());

            object value;
            bool success = dt.TryGetPropertyValue("BirthdayA", out value);
            Assert.True(success);
            DateTime dateTime = Assert.IsType<DateTime>(value);
            Assert.Equal(DateTimeKind.Unspecified, dateTime.Kind);
            Assert.Equal(new DateTime(2098, 12, 31, 17, 2, 3), dateTime);

            success = dt.TryGetPropertyValue("BirthdayB", out value);
            Assert.True(success);
            Assert.Null(value);

            return Updated(dt);
        }

        public IHttpActionResult GetBirthdayD(int key)
        {
            DateTimeModel dtm = db.DateTimes.FirstOrDefault(e => e.Id == key);
            if (dtm == null)
            {
                return NotFound();
            }

            return Ok(dtm.BirthdayD);
        }

        [HttpGet]
        public DateTime CalcBirthday(DateTimeOffset dto)
        {
            Assert.Equal(DateTimeOffset.Parse("2012-12-22T01:02:03Z"), dto);
            return new DateTime(1978, 11, 15, 0, 12, 0, DateTimeKind.Utc);
        }
    }

    class DateTimeModelContext
    {
        private static IList<DateTimeModel> _dateTimes;

        static DateTimeModelContext()
        {
            DateTime dt = new DateTime(2014, 12, 31, 20, 12, 30, DateTimeKind.Utc);

            _dateTimes = Enumerable.Range(1, 5).Select(i =>
                new DateTimeModel
                {
                    Id = i,
                    BirthdayA = dt.AddYears(i),
                    BirthdayB = dt.AddMonths(i),
                    BirthdayC = new List<DateTime> { dt.AddYears(i), dt.AddMonths(i), dt.AddDays(i) },
                    BirthdayD = new List<DateTime?>
                    {
                        dt.AddYears(2 * i), null, dt.AddMonths(2 * i)
                    }
                }).ToList();
        }

        public IEnumerable<DateTimeModel> DateTimes { get { return _dateTimes; } }
    }
}
