﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NI;
using NUnit.Framework;
using Symphony.Core;

namespace IntegrationTests
{
    [TestFixture]
    public class NIIntegration
    {
        const double MAX_VOLTAGE_DIFF = 0.1; //Volts. This is completely arbitrary and dependent on the quality of the patch cable. Just something "close" to 0.
        
        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            Logging.ConfigureConsole();
        }

        [Test, Sequential]
        [Timeout(30 * 1000)]
        public void HighBandwidth(
            [Values(1000, 10000, 25000, 50000)] decimal sampleRate,
            [Values(1, 1, 1, 1)] int nEpochs,
            [Values(4, 4, 4, 4)] int nOut,
            [Values(8, 8, 8, 4)] int nIn
            )
        {
            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);

            Assert.That(NIDAQController.AvailableControllers().Count(), Is.GreaterThan(0));

            foreach (var daq in NIDAQController.AvailableControllers())
            {

                const double epochDuration = 10; //s

                daq.InitHardware();
                try
                {
                    daq.SampleRate = new Measurement(sampleRate, "Hz");

                    var controller = new Controller { Clock = daq.Clock, DAQController = daq };

                    var outDevices = Enumerable.Range(0, nOut)
                        .Select(i =>
                        {
                            var dev0 = new UnitConvertingExternalDevice("Device_OUT_" + i, "Manufacturer", controller,
                                                                        new Measurement(0, "V"))
                            {
                                MeasurementConversionTarget = "V",
                                Clock = daq.Clock,
                                OutputSampleRate = daq.SampleRate
                            };
                            dev0.BindStream((IDAQOutputStream)daq.GetStreams("ao" + i).First());

                            return dev0;
                        })
                                    .ToList();


                    var inDevices = Enumerable.Range(0, nIn)
                        .Select(i =>
                        {
                            var dev0 = new UnitConvertingExternalDevice("Device_IN_" + i, "Manufacturer", controller,
                                                                        new Measurement(0, "V"))
                            {
                                MeasurementConversionTarget = "V",
                                Clock = daq.Clock,
                                InputSampleRate = daq.SampleRate
                            };
                            dev0.BindStream((IDAQInputStream)daq.GetStreams("ai" + i).First());

                            return dev0;
                        })
                                    .ToList();

                    for (int j = 0; j < nEpochs; j++)
                    {
                        // Setup Epoch
                        var e = new Epoch("NIIntegration");

                        var nSamples = (int)TimeSpan.FromSeconds(epochDuration).Samples(daq.SampleRate);
                        IList<IMeasurement> stimData = Enumerable.Range(0, nSamples)
                                                                   .Select(i => new Measurement((decimal)(8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V") as IMeasurement)
                                                                   .ToList();

                        foreach (var outDev in outDevices)
                        {
                            e.Stimuli[outDev] = new RenderedStimulus((string)"RenderedStimulus", (IDictionary<string, object>)new Dictionary<string, object>(),
                                                        (IOutputData)new OutputData(stimData, daq.SampleRate));

                            e.Backgrounds[outDev] = new Background(new Measurement(0, "V"), daq.SampleRate);
                        }

                        foreach (var inDev in inDevices)
                        {
                            e.Responses[inDev] = new Response();
                        }


                        //Run single epoch
                        var fakeEpochPersistor = new FakeEpochPersistor();

                        controller.RunEpoch(e, fakeEpochPersistor);



                        Assert.That((bool)e.StartTime, Is.True);
                        Assert.That((DateTimeOffset)e.StartTime, Is.LessThanOrEqualTo(controller.Clock.Now));
                        Assert.That(fakeEpochPersistor.PersistedEpochs, Contains.Item(e));

                    }

                }
                finally
                {
                    if (daq.IsHardwareReady)
                        daq.CloseHardware();
                }

            }
        }

        [Test]
        [Timeout(30 * 1000)]
        public void RenderedStimulus(
            [Values(1000, 10000, 20000, 50000)] double sampleRate,
            [Values(2)] int nEpochs
            )
        {
            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);

            Assert.That(NIDAQController.AvailableControllers().Count(), Is.GreaterThan(0));
            foreach (var daq in NIDAQController.AvailableControllers())
            {

                const double epochDuration = 5; //s

                daq.InitHardware();
                try
                {
                    daq.SampleRate = new Measurement((decimal)sampleRate, "Hz");

                    var controller = new Controller { Clock = daq.Clock, DAQController = daq };

                    var dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller, new Measurement(0, "V"))
                    {
                        MeasurementConversionTarget = "V",
                        Clock = daq.Clock,
                        OutputSampleRate = daq.SampleRate,
                        InputSampleRate = daq.SampleRate
                    };
                    dev0.BindStream((IDAQOutputStream)daq.GetStreams("ao0").First());
                    dev0.BindStream((IDAQInputStream)daq.GetStreams("ai0").First());

                    var dev1 = new UnitConvertingExternalDevice("Device1", "Manufacturer", controller, new Measurement(0, "V"))
                    {
                        MeasurementConversionTarget = "V",
                        Clock = daq.Clock,
                        OutputSampleRate = daq.SampleRate,
                        InputSampleRate = daq.SampleRate
                    };
                    dev1.BindStream((IDAQOutputStream)daq.GetStreams("ao1").First());
                    dev1.BindStream((IDAQInputStream)daq.GetStreams("ai1").First());

                    for (int j = 0; j < nEpochs; j++)
                    {
                        // Setup Epoch
                        var e = new Epoch("NIIntegration" + j);

                        var nSamples = (int)TimeSpan.FromSeconds(epochDuration).Samples(daq.SampleRate);
                        IList<IMeasurement> stimData = (IList<IMeasurement>)Enumerable.Range(0, nSamples)
                                                                   .Select(i => new Measurement((decimal)(8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V") as IMeasurement)
                                                                   .ToList();

                        e.Stimuli[dev0] = new RenderedStimulus((string)"RenderedStimulus", (IDictionary<string, object>)new Dictionary<string, object>(),
                                                        (IOutputData)new OutputData(stimData, daq.SampleRate));
                        e.Responses[dev0] = new Response();

                        e.Stimuli[dev1] = new RenderedStimulus((string)"RenderedStimulus", (IDictionary<string, object>)new Dictionary<string, object>(),
                                                       (IOutputData)new OutputData(Enumerable.Range(0, nSamples)
                                                                                        .Select(i => new Measurement((decimal)(8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V"))
                                                                                        .ToList(),
                                                                                    daq.SampleRate));
                        e.Responses[dev1] = new Response();


                        //Run single epoch
                        var fakeEpochPersistor = new FakeEpochPersistor();

                        controller.RunEpoch(e, fakeEpochPersistor);



                        Assert.That((bool)e.StartTime, Is.True);
                        Assert.That((DateTimeOffset)e.StartTime, Is.LessThanOrEqualTo(controller.Clock.Now));
                        Assert.That(e.Responses[dev0].Duration, Is.EqualTo(((TimeSpan) e.Duration))
                                                                  .Within(TimeSpanExtensions.FromSamples(1,
                                                                                                         daq.
                                                                                                             SampleRate)));
                        Assert.That(e.Responses[dev1].Duration, Is.EqualTo(((TimeSpan) e.Duration))
                                                                  .Within(TimeSpanExtensions.FromSamples(1,
                                                                                                         daq.
                                                                                                             SampleRate)));
                        Assert.That(fakeEpochPersistor.PersistedEpochs, Contains.Item(e));

                        var failures0 =
                            e.Responses[dev0].Data.Select(
                                (t, i) => new { index = i, diff = t.QuantityInBaseUnits - stimData[i].QuantityInBaseUnits })
                                .Where(dif => Math.Abs(dif.diff) > (decimal)MAX_VOLTAGE_DIFF);

                        foreach (var failure in failures0.Take(10))
                            Console.WriteLine("{0}: {1}", failure.index, failure.diff);

                        Assert.That(failures0.Count(), Is.LessThanOrEqualTo(0));
                    }

                }
                finally
                {
                    if (daq.IsHardwareReady)
                        daq.CloseHardware();
                }

            }
        }

        [Test]
        [Timeout(30 * 1000)]
        public void ContinuousAcquisition(
            [Values(1000, 10000, 20000, 50000)] double sampleRate,
            [Values(20)] int nEpochs)
        {
            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);

            Assert.That(NIDAQController.AvailableControllers().Count(), Is.GreaterThan(0));
            foreach (var daq in NIDAQController.AvailableControllers())
            {
                const double epochDuration = 1; //s

                try
                {
                    daq.InitHardware();
                    daq.SampleRate = new Measurement((decimal)sampleRate, "Hz");

                    var controller = new Controller { Clock = daq.Clock, DAQController = daq };

                    var dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller, new Measurement(0, "V"))
                    {
                        MeasurementConversionTarget = "V",
                        Clock = daq.Clock,
                        OutputSampleRate = daq.SampleRate,
                        InputSampleRate = daq.SampleRate
                    };
                    dev0.BindStream((IDAQOutputStream)daq.GetStreams("ao0").First());
                    dev0.BindStream((IDAQInputStream)daq.GetStreams("ai0").First());

                    var dev1 = new UnitConvertingExternalDevice("Device1", "Manufacturer", controller, new Measurement(0, "V"))
                    {
                        MeasurementConversionTarget = "V",
                        Clock = daq.Clock,
                        OutputSampleRate = daq.SampleRate,
                        InputSampleRate = daq.SampleRate
                    };
                    dev1.BindStream((IDAQOutputStream)daq.GetStreams("ao1").First());
                    dev1.BindStream((IDAQInputStream)daq.GetStreams("ai1").First());

                    var nDAQStarts = 0;
                    daq.Started += (evt, args) =>
                    {
                        nDAQStarts++;
                    };

                    var completedEpochs = new Queue<Epoch>();
                    controller.CompletedEpoch += (evt, args) =>
                    {
                        completedEpochs.Enqueue(args.Epoch);
                        if (completedEpochs.Count >= nEpochs)
                        {
                            controller.RequestStop();
                        }
                    };

                    var epochs = new Queue<Epoch>();
                    var nSamples = (int)TimeSpan.FromSeconds(epochDuration).Samples(daq.SampleRate);

                    // Triangle wave
                    var data = Enumerable.Range(0, nSamples / 2)
                                         .Select(k => new Measurement(k / (nSamples / 2.0 / 10.0), "V") as IMeasurement)
                                         .ToList();
                    var stimData = data.Concat(Enumerable.Reverse(data)).ToList();

                    var fakeEpochPersistor = new FakeEpochPersistor();

                    bool start = true;

                    for (int j = 0; j < nEpochs; j++)
                    {
                        var e = new Epoch("NIIntegration" + j);

                        e.Stimuli[dev0] = new RenderedStimulus("Stim",
                                                               new Dictionary<string, object>(),
                                                               new OutputData(stimData, daq.SampleRate));
                        e.Responses[dev0] = new Response();

                        e.SetBackground(dev1, new Measurement(1, "V"), daq.SampleRate);
                        e.Responses[dev1] = new Response();

                        epochs.Enqueue(e);

                        controller.EnqueueEpoch(e);

                        if (start)
                        {
                            controller.StartAsync(fakeEpochPersistor);
                            start = false;
                        }
                    }

                    while (controller.IsRunning)
                    {
                        Thread.Sleep(1);
                    }

                    controller.WaitForCompletedEpochTasks();
                    var stopTime = controller.Clock.Now;

                    Assert.That(nDAQStarts <= 1, "Epochs did not run continuously");

                    Assert.AreEqual(epochs, completedEpochs);
                    Assert.AreEqual(fakeEpochPersistor.PersistedEpochs, completedEpochs);

                    DateTimeOffset prevTime = completedEpochs.First().StartTime;
                    foreach (var e in completedEpochs)
                    {
                        Assert.That((bool)e.StartTime, Is.True);
                        Assert.That((DateTimeOffset)e.StartTime, Is.GreaterThanOrEqualTo(prevTime));
                        Assert.That((DateTimeOffset)e.StartTime, Is.LessThanOrEqualTo(stopTime));

                        Assert.That(e.Responses[dev0].Duration, Is.EqualTo(((TimeSpan)e.Duration))
                                              .Within(TimeSpanExtensions.FromSamples(1, daq.SampleRate)));

                        Assert.That(e.Responses[dev1].Duration, Is.EqualTo(((TimeSpan)e.Duration))
                                                                      .Within(TimeSpanExtensions.FromSamples(1, daq.SampleRate)));

                        var failures0 =
                            e.Responses[dev0].Data.Select(
                                (t, i) => new { index = i, diff = t.QuantityInBaseUnits - stimData[i].QuantityInBaseUnits })
                                             .Where(dif => Math.Abs(dif.diff) > (decimal)MAX_VOLTAGE_DIFF);

                        foreach (var failure in failures0.Take(10))
                            Console.WriteLine("{0}: {1}", failure.index, failure.diff);

                        /*
                         * According to Telly @ NI, a patch cable may introduce 3-4 offset points
                         */
                        Assert.That(failures0.Count(), Is.LessThanOrEqualTo(4));

                        prevTime = e.StartTime;
                    }
                }
                finally
                {
                    if (daq.IsHardwareReady)
                    {
                        daq.CloseHardware();
                    }
                }
            }
        }


        [Test]
        [Timeout(10 * 1000)]
        public void ShouldSetStreamBackgroundOnStop(
            [Values(10000, 20000)] double sampleRate
            )
        {
            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);

            Assert.That(NIDAQController.AvailableControllers().Count(), Is.GreaterThan(0));

            foreach (var daq in NIDAQController.AvailableControllers())
            {

                const double epochDuration = 1; //s
                //Configure DAQ
                daq.InitHardware();
                try
                {
                    daq.SampleRate = new Measurement((decimal)sampleRate, "Hz");

                    var controller = new Controller();
                    controller.Clock = daq.Clock;
                    controller.DAQController = daq;


                    const decimal expectedBackgroundVoltage = -3.2m;
                    var expectedBackground = new Measurement(expectedBackgroundVoltage, "V");
                    var dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller, expectedBackground)
                    {
                        MeasurementConversionTarget = "V",
                        OutputSampleRate = daq.SampleRate,
                        InputSampleRate = daq.SampleRate
                    };
                    dev0.BindStream(daq.GetStreams("ao0").First() as IDAQOutputStream);
                    dev0.BindStream(daq.GetStreams("ai0").First() as IDAQInputStream);
                    dev0.Clock = daq.Clock;

                    controller.DiscardedEpoch += (c, args) => Console.WriteLine("Discarded epoch: " + args.Epoch);

                    // Setup Epoch
                    var e = new Epoch("NIIntegration");

                    var nSamples = (int)TimeSpanExtensions.Samples(TimeSpan.FromSeconds(epochDuration), daq.SampleRate);
                    IList<IMeasurement> stimData = (IList<IMeasurement>)Enumerable.Range(0, nSamples)
                                                               .Select(i => new Measurement((decimal)(8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V") as IMeasurement)
                                                               .ToList();

                    var stim = new RenderedStimulus((string)"RenderedStimulus", (IDictionary<string, object>)new Dictionary<string, object>(),
                                                    (IOutputData)new OutputData(stimData, daq.SampleRate, false));
                    e.Stimuli[dev0] = stim;
                    e.Responses[dev0] = new Response();
                    e.Backgrounds[dev0] = new Background(expectedBackground, daq.SampleRate);

                    //Run single epoch
                    var fakeEpochPersistor = new FakeEpochPersistor();

                    controller.RunEpoch(e, fakeEpochPersistor);

                    Thread.Sleep(TimeSpan.FromMilliseconds(100)); //allow DAC to settle


                    var actual = ((NIDAQController)controller.DAQController).ReadStreamAsync(
                        daq.GetStreams("ai0").First() as IDAQInputStream);

                    //Should be within +/- 0.025 volts
                    Assert.That(actual.Data.First().QuantityInBaseUnits, Is.InRange(expectedBackground.QuantityInBaseUnits - (decimal)0.025,
                        expectedBackground.QuantityInBaseUnits + (decimal)0.025));
                }
                finally
                {
                    if (daq.IsHardwareReady)
                        daq.CloseHardware();
                }

            }
        }

        [Test]
        [Timeout(12 * 1000)]
        public void SealLeak(
            [Values(10000, 20000, 50000)] double sampleRate
            )
        {
            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);

            Assert.That(NIDAQController.AvailableControllers().Count(), Is.GreaterThan(0));

            foreach (var daq in NIDAQController.AvailableControllers())
            {
                const double epochDuration = 10; //s

                //Configure DAQ
                daq.InitHardware();
                try
                {
                    daq.SampleRate = new Measurement((decimal)sampleRate, "Hz");

                    var controller = new Controller();
                    controller.Clock = daq.Clock;
                    controller.DAQController = daq;


                    const decimal expectedBackgroundVoltage = 3.2m;
                    var expectedBackground = new Measurement(expectedBackgroundVoltage, "V");
                    var dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller, expectedBackground)
                    {
                        MeasurementConversionTarget = "V",
                        Clock = daq.Clock,
                        OutputSampleRate = daq.SampleRate,
                        InputSampleRate = daq.SampleRate
                    };
                    dev0.BindStream(daq.GetStreams("ao0").First() as IDAQOutputStream);
                    dev0.BindStream(daq.GetStreams("ai0").First() as IDAQInputStream);

                    controller.DiscardedEpoch += (c, args) => Console.WriteLine("Discarded epoch: " + args.Epoch);

                    // Setup Epoch
                    var e = new Epoch("NIIntegration");

                    NIDAQController cDAQ = daq;
                    var stim = new DelegatedStimulus("TEST_ID", "V", cDAQ.SampleRate, new Dictionary<string, object>(),
                                                     (parameters, duration) =>
                                                     DataForDuration(duration, cDAQ.SampleRate),
                                                     parameters => Option<TimeSpan>.None()
                        );

                    e.Stimuli[dev0] = stim;
                    e.Backgrounds[dev0] = new Background(expectedBackground, daq.SampleRate);

                    //Run single epoch
                    var fakeEpochPersistor = new FakeEpochPersistor();

                    new TaskFactory().StartNew(() =>
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(epochDuration));
                        controller.RequestStop();
                    },
                                               TaskCreationOptions.LongRunning
                        );

                    controller.RunEpoch(e, fakeEpochPersistor);
                }
                finally
                {
                    if (daq.IsHardwareReady)
                        daq.CloseHardware();
                }
            }
        }

        private static IOutputData DataForDuration(TimeSpan blockDuration, IMeasurement sampleRate)
        {
            ulong nSamples = blockDuration.Samples(sampleRate);

            var samples = Enumerable.Range(0, (int)nSamples).Select(i => new Measurement(1, "V")).ToList();

            return new OutputData(samples, sampleRate, false);
        }

        [Test]
        [Timeout((5 * 60) * 1000)]
        public void LongEpochPersistence(
            [Values(5, 60)] double epochDuration, //seconds
            [Values(2)] int nEpochs
            )
        {
            const decimal sampleRate = 10000m;

            const string h5Path = "..\\..\\..\\LongEpochPersistence.h5";
            if (File.Exists(h5Path))
                File.Delete(h5Path);

            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);

            Assert.That(NIDAQController.AvailableControllers().Count(), Is.GreaterThan(0));

            using (var persistor = H5EpochPersistor.Create(h5Path))
            {
                persistor.AddSource("source", null);
            }

            foreach (var daq in NIDAQController.AvailableControllers())
            {

                try
                {

                    daq.InitHardware();
                    daq.SampleRate = new Measurement(sampleRate, "Hz");

                    var controller = new Controller { Clock = daq.Clock, DAQController = daq };

                    var dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller,
                                                                new Measurement(0, "V"))
                    {
                        MeasurementConversionTarget = "V",
                        Clock = daq.Clock,
                        OutputSampleRate = daq.SampleRate,
                        InputSampleRate = daq.SampleRate
                    };
                    dev0.BindStream((IDAQOutputStream)daq.GetStreams("ao0").First());
                    dev0.BindStream((IDAQInputStream)daq.GetStreams("ai0").First());

                    for (int j = 0; j < nEpochs; j++)
                    {
                        // Setup Epoch
                        var e = new Epoch("NIIntegration");

                        var nSamples = (int)TimeSpan.FromSeconds(epochDuration).Samples(daq.SampleRate);
                        IList<IMeasurement> stimData = (IList<IMeasurement>)Enumerable.Range(0, nSamples)
                                                                                 .Select(
                                                                                     i =>
                                                                                     new Measurement(
                                                                                         (decimal)
                                                                                         (8 *
                                                                                          Math.Sin(((double)i) /
                                                                                                   (nSamples / 10.0))),
                                                                                         "V") as IMeasurement)
                                                                                 .ToList();

                        e.Stimuli[dev0] = new RenderedStimulus((string)"RenderedStimulus", (IDictionary<string, object>)new Dictionary<string, object>(),
                                                               (IOutputData)new OutputData(stimData, daq.SampleRate));
                        e.Responses[dev0] = new Response();
                        e.Backgrounds[dev0] = new Background(new Measurement(0, "V"), daq.SampleRate);



                        //Run single epoch
                        using (var persistor = new H5EpochPersistor(h5Path))
                        {
                            var source = persistor.Experiment.Sources.First();

                            persistor.BeginEpochGroup("label", source, DateTimeOffset.Now);
                            persistor.BeginEpochBlock(e.ProtocolID, e.ProtocolParameters, DateTimeOffset.Now);

                            controller.RunEpoch(e, persistor);

                            persistor.EndEpochBlock(DateTimeOffset.Now);
                            persistor.EndEpochGroup();
                        }


                        Assert.That((bool)e.StartTime, Is.True);
                        Assert.That((DateTimeOffset)e.StartTime, Is.LessThanOrEqualTo(controller.Clock.Now));
                        Assert.That(e.Responses[dev0].Duration, Is.EqualTo(((TimeSpan)e.Duration))
                                                                    .Within(TimeSpanExtensions.FromSamples(1,
                                                                                                           daq.
                                                                                                               SampleRate)));
                        //Assert.That(e.Responses[dev1].Duration, Is.EqualTo(((TimeSpan) e.Duration))
                        //                                            .Within(TimeSpanExtensions.FromSamples(1,
                        //                                                                                   daq.
                        //                                                                                       SampleRate)));
                    }
                }
                finally
                {
                    if (File.Exists(h5Path))
                        File.Delete(h5Path);

                    if (daq.IsHardwareReady)
                        daq.CloseHardware();

                }
            }
        }

    }

}
