﻿using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;

namespace Symphony.Core
{
    using NUnit.Framework;

    [TestFixture]
    class DAQStreamTests
    {
        private const string UNUSED_NAME = "UNUSED_NAME";

        [Test]
        [ExpectedException(typeof(ArgumentException),
            ExpectedMessage = "Illegal SampleRate")]
        public void OutputStreamSetSampleRateNegativeRate()
        {
            DAQOutputStream s = new DAQOutputStream("");

            s.SampleRate = new Measurement(-1, "Hz");
        }

        [Test]
        [ExpectedException(typeof(ArgumentException),
            ExpectedMessage = "Illegal SampleRate")]
        public void InputStreamSetSampleRateNegativeRate()
        {
            DAQInputStream s = new DAQInputStream("");

            s.SampleRate = new Measurement(-1, "Hz");
        }

        [Test]
        [ExpectedException(typeof(ArgumentException),
            ExpectedMessage = "Illegal SampleRate")]
        public void OutputStreamSetSampleRateUnits()
        {
            DAQOutputStream s = new DAQOutputStream("");

            s.SampleRate = new Measurement(10, "A");
        }

        [Test]
        [ExpectedException(typeof(ArgumentException),
            ExpectedMessage = "Illegal SampleRate")]
        public void InputStreamSetSampleRateUnits()
        {
            DAQInputStream s = new DAQInputStream("");

            s.SampleRate = new Measurement(10, "A");
        }

        [Test]
        public void InputStreamShouldReadValue()
        {
            var controller = Substitute.For<IDAQController>();
            var s = new DAQInputStream("test", controller)
                {
                    MeasurementConversionTarget = "V"
                };

            var expected = new InputData(Enumerable.Repeat(new Measurement(1, "V"), 1), null, DateTimeOffset.Now);

            controller.ReadStream(s).Returns(expected);

            var actual = s.Read();

            controller.Received().ReadStream(s);
        }

        [Test]
        public void StreamSetSampleRate()
        {
            DAQInputStream s1 = new DAQInputStream("");
            Measurement expected = new Measurement(10, "Hz");

            s1.SampleRate = expected;
            Assert.AreEqual(expected, s1.SampleRate);

            DAQOutputStream s2 = new DAQOutputStream("");
            s2.SampleRate = expected;

            Assert.AreEqual(expected, s2.SampleRate);

        }

        [Test]
        [ExpectedException(typeof(DAQException))]
        public void ThrowsIfDeviceOutputDataSampleRateMismatch()
        {
            IList<IOutputData> data = new List<IOutputData>(3);

            List<IMeasurement> measurements = new List<IMeasurement>(2);
            measurements.Add(new Measurement(1, "V"));
            measurements.Add(new Measurement(2, "V"));

            IMeasurement sampleRate = new Measurement(1000, "Hz");
            data.Add(new OutputData(measurements,
                sampleRate, false));
            data.Add(new OutputData(measurements,
                sampleRate, false));

            data.Add(new OutputData(measurements,
                sampleRate, false));


            DAQOutputStream s = new DAQOutputStream("OUT");
            s.MeasurementConversionTarget = measurements.First().BaseUnits;

            s.SampleRate = new Measurement(sampleRate.QuantityInBaseUnits * 2, "Hz");

            var outData = new Dictionary<IDAQOutputStream, Queue<IOutputData>>(1);
            outData[s] = new Queue<IOutputData>(data);

            TestDevice outDevice = new TestDevice("OUT-DEVICE", outData);
            outDevice.Controller = new Controller();
            s.Devices.Add(outDevice);

            s.PullOutputData(new TimeSpan(100));
        }

        [Test]
        public void PullsDataFromExternalDevice()
        {
            IList<IOutputData> data;
            DAQOutputStream s;
            OutputStreamFixture(out data, out s, 3);

            foreach (IOutputData expected in data)
            {
                var actual = s.PullOutputData(TimeSpan.FromSeconds(1));
                CollectionAssert.AreEqual(expected.Data, actual.Data);
                Assert.AreEqual(expected.SampleRate, actual.SampleRate);
                if (!actual.IsLast)
                    Assert.True(s.HasMoreData);
            }
        }

        [Test]
        public void PullsDataFromMultipleExternalDevices()
        {
            const int numDevices = 4;

            IList<IOutputData> data;
            DAQOutputStream s;
            OutputStreamFixture(out data, out s, 3);

            for (int i = 1; i < numDevices; i++)
            {
                var outData = new Dictionary<IDAQOutputStream, Queue<IOutputData>>(1);
                outData[s] = new Queue<IOutputData>(data);

                var outDevice = new TestDevice("OUT-DEVICE" + i, outData)
                {
                    Controller = new Controller(),
                    MeasurementConversionTarget = "V"
                };

                s.Devices.Add(outDevice);
            }
            
            foreach (IOutputData d in data)
            {
                var measurements = Measurement.FromArray(d.Data.Select(m => m.QuantityInBaseUnits*numDevices).ToArray(), d.Data.BaseUnits());
                var expected = new OutputData(measurements, d.SampleRate, d.IsLast);

                var actual = s.PullOutputData(TimeSpan.FromSeconds(1));
                CollectionAssert.AreEqual(expected.Data, actual.Data);
                Assert.AreEqual(expected.SampleRate, actual.SampleRate);
                if (!actual.IsLast)
                    Assert.True(s.HasMoreData);
            }
        }

        private const string UNUSED_DEVICE_MANUFACTURER = "DEVICECO";

        [Test]
        public void BackgroundProxiesExternalDevice()
        {
            
            var background = new Measurement(1.3m, "units");
            var dev = new UnitConvertingExternalDevice(UNUSED_NAME, UNUSED_DEVICE_MANUFACTURER, background)
                          {
                              MeasurementConversionTarget = "units"
                          };

            var stream = new DAQOutputStream(UNUSED_NAME);
            stream.Devices.Add(dev);

            Assert.AreEqual(background.QuantityInBaseUnits, stream.Background.QuantityInBaseUnits);
        }

        private static void OutputStreamFixture(out IList<IOutputData> data, out DAQOutputStream s, int numData)
        {
            data = new List<IOutputData>(3);

            var measurements = new List<IMeasurement>(2) {new Measurement(1, "V"), new Measurement(2, "V")};

            var sampleRate = new Measurement(1000, "Hz");
            for (int i = 0; i < numData; i++)
            {
                bool last = i == numData - 1 ? true : false;

                IOutputData outputData = new OutputData(measurements,
                                                        sampleRate,
                                                        last);
                data.Add(outputData);
            }


            s = new DAQOutputStream("OUT")
                    {
                        MeasurementConversionTarget = measurements.First().BaseUnits,
                        SampleRate = sampleRate
                    };

            var outData = new Dictionary<IDAQOutputStream, Queue<IOutputData>>(1);
            outData[s] = new Queue<IOutputData>(data);

            var outDevice = new TestDevice("OUT-DEVICE", outData)
                                {
                                    Controller = new Controller(),
                                    MeasurementConversionTarget = "V"
                                };

            s.Devices.Add(outDevice);
        }

        [Test]
        public void InactiveStreamDoesNotHaveMoreData()
        {
            DAQOutputStream s = new DAQOutputStream("OUT");
            Assert.False(s.Active);
            Assert.False(s.HasMoreData);
        }

        [Test]
        public void ConstructedStreamHasMoreData()
        {
            IList<IOutputData> data;
            DAQOutputStream s;
            OutputStreamFixture(out data, out s, 1);

            Assert.True(s.Active);
            Assert.True(s.HasMoreData);

        }

        [Test]
        public void DoesNotHaveMoreDataAfterPullingLastData()
        {
            IList<IOutputData> data;
            DAQOutputStream s;
            OutputStreamFixture(out data, out s, 1);

            var actual = s.PullOutputData(TimeSpan.FromSeconds(10));
            Assert.True(actual.IsLast);
            Assert.False(s.HasMoreData);

        }

        [Test]
        public void ResetHasMoreData()
        {
            IList<IOutputData> data;
            DAQOutputStream s;
            OutputStreamFixture(out data, out s, 1);

            var actual = s.PullOutputData(TimeSpan.FromSeconds(10));
            Assert.False(s.HasMoreData);

            s.Reset();

            Assert.True(s.HasMoreData);

        }

        [Test]
        public void PushesDataToDevices()
        {
            String units = "V";
            DAQInputStream s = new DAQInputStream("IN");
            s.MeasurementConversionTarget = units;

            TestDevice inDevice = new TestDevice("IN-DEVICE", null);
            inDevice.MeasurementConversionTarget = units;

            inDevice.BindStream(s);

            IList<IMeasurement> data = new List<IMeasurement>(2);
            data.Add(new Measurement(1, units));
            data.Add(new Measurement(2, units));

            IMeasurement sampleRate = new Measurement(1000, "Hz");
            DateTimeOffset time = DateTimeOffset.Now;

            IInputData inData = new InputData(data, sampleRate, time);
            s.PushInputData(inData);

            IInputData actual = inDevice.InputData[s].First();

            CollectionAssert.AreEqual(inData.Data, actual.Data);
            Assert.AreEqual(inData.InputTime, actual.InputTime);
        }


        [Test]
        [ExpectedException(typeof(DAQException))]
        public void PullWithoutDevice()
        {
            DAQOutputStream s = new DAQOutputStream("OUT");
            s.PullOutputData(new TimeSpan(1));
        }

        [Test]
        public void ImplementsTimelineProducer()
        {
            Assert.True(typeof(DAQInputStream).FindInterfaces((t, criteria) => { return true; }, null).Contains(typeof(ITimelineProducer)));
            Assert.True(typeof(DAQOutputStream).FindInterfaces((t, criteria) => { return true; }, null).Contains(typeof(ITimelineProducer)));

        }

        [Test]
        public void OutputStreamProvidesBackground()
        {

            IList<IOutputData> data;
            DAQOutputStream s;
            OutputStreamFixture(out data, out s, 1);
            
            Assert.NotNull(s.Devices.First().Background);
            Assert.AreEqual(s.Devices.First().OutputBackground, s.Background);
        }

        [Test]
        public void OutputStreamShouldPropagateOutputConfiguration()
        {
            var s = new DAQOutputStream("test");
            var device = Substitute.For<IExternalDevice>();

            DateTimeOffset time = DateTime.Now;
            var config = new List<IPipelineNodeConfiguration>();

            s.Devices.Add(device);

            s.DidOutputData(time, TimeSpan.FromSeconds(0.1), config);

            device.Received().DidOutputData(s, time, TimeSpan.FromSeconds(0.1), config);
        }

        [Test]
        public void OutputStreamShouldApplyBackground()
        {
            var controller = Substitute.For<IDAQController>();
            var s = new DAQOutputStream("test", controller);

            s.ApplyBackground();

            controller.Received().ApplyStreamBackground(s);
        }
    }
}
