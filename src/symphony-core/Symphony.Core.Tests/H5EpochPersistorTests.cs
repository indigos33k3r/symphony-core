﻿using System;
using System.Collections.Generic;
using System.Linq;
using HDF5DotNet;
using NUnit.Framework;

namespace Symphony.Core
{
    class H5EpochPersistorTests
    {
        const string TEST_FILE = "test_experiment.h5";
        const string TEST_PURPOSE = "the test purpose here";

        private H5EpochPersistor persistor;
        private DateTimeOffset startTime;

        [SetUp]
        public void Setup()
        {
            //if (System.IO.File.Exists(TEST_FILE))
            //    System.IO.File.Delete(TEST_FILE);

            startTime = DateTimeOffset.Now;
            persistor = H5EpochPersistor.Create(TEST_FILE, TEST_PURPOSE, startTime);
        }

        [TearDown]
        public void Teardown()
        {
            try
            {
                persistor.Close(DateTimeOffset.Now);
            }
            finally
            {
                if (System.IO.File.Exists(TEST_FILE))
                    System.IO.File.Delete(TEST_FILE);
            }
        }

        [Test]
        public void ShouldContainExperiment()
        {
            var experiment = persistor.Experiment;
            Assert.AreEqual(TEST_PURPOSE, experiment.Purpose);
            Assert.AreEqual(startTime, experiment.StartTime);
            Assert.IsNull(experiment.EndTime);
            Assert.AreEqual(0, experiment.Devices.Count());
            Assert.AreEqual(0, experiment.Sources.Count());
            Assert.AreEqual(0, experiment.EpochGroups.Count());
        }

        [Test]
        public void ShouldSetExperimentEndTimeOnClose()
        {
            var time = DateTimeOffset.Now;
            persistor.Close(time);
            persistor = new H5EpochPersistor(TEST_FILE);
            Assert.AreEqual(time, persistor.Experiment.EndTime);
        }

        [Test]
        public void ShouldNotAllowDeletingExperiment()
        {
            var experiment = persistor.Experiment;
            Assert.Throws(typeof (InvalidOperationException), () => persistor.Delete(experiment));
        }

        [Test]
        public void ShouldAddAndRemoveProperties()
        {
            var expected = new Dictionary<string, object>();

            var experiment = persistor.Experiment;

            for (int i = 0; i < 10; i++)
            {
                experiment.AddProperty(i.ToString(), i);
                expected.Add(i.ToString(), i);
            }
            CollectionAssert.AreEquivalent(expected, experiment.Properties);

            for (int i = 0; i < 5; i++)
            {
                experiment.RemoveProperty(i.ToString());
                expected.Remove(i.ToString());
            }
            CollectionAssert.AreEquivalent(expected, experiment.Properties);
        }

        [Test]
        public void ShouldAddAndRemoveKeywords()
        {
            var expected = new HashSet<string>();

            var experiment = persistor.Experiment;

            for (int i = 0; i < 10; i++)
            {
                experiment.AddKeyword(i.ToString());
                expected.Add(i.ToString());
            }
            CollectionAssert.AreEquivalent(expected, experiment.Keywords);

            for (int i = 0; i < 5; i++)
            {
                experiment.RemoveKeyword(i.ToString());
                expected.Remove(i.ToString());
            }
            CollectionAssert.AreEquivalent(expected, experiment.Keywords);
        }

        [Test]
        public void ShouldAddNotes()
        {
            var expected = new List<INote>();

            var experiment = persistor.Experiment;

            for (int i = 0; i < 100; i++)
            {
                DateTimeOffset time = DateTimeOffset.Now;
                string text = "This is note number " + i;

                var n = experiment.AddNote(time, "This is note number " + i);
                Assert.AreEqual(time, n.Time);
                Assert.AreEqual(text, n.Text);

                expected.Add(n);
            }
            CollectionAssert.AreEquivalent(expected, experiment.Notes);
        }

        [Test]
        public void ShouldAddLongNote()
        {
            var text = new string('*', 500);
            var note = persistor.Experiment.AddNote(DateTimeOffset.Now, text);
            Assert.AreEqual(text, note.Text);
        }

        [Test]
        public void ShouldAddDevice()
        {
            var name = "axopatch";
            var manufacturer = "axon";
            var dev = persistor.AddDevice(name, manufacturer);

            Assert.AreEqual(name, dev.Name);
            Assert.AreEqual(manufacturer, dev.Manufacturer);

            CollectionAssert.AreEquivalent(new[] {dev}, persistor.Experiment.Devices);
        }

        [Test]
        public void ShouldNotAddInvalidDevice()
        {
            Assert.Throws(typeof (ArgumentException), () => persistor.AddDevice(null, null));
            Assert.Throws(typeof (ArgumentException), () => persistor.AddDevice("", ""));
            Assert.Throws(typeof (ArgumentException), () => persistor.AddDevice("dev", null));
            Assert.Throws(typeof (ArgumentException), () => persistor.AddDevice(null, "man"));
            Assert.AreEqual(0, persistor.Experiment.Devices.Count());
        }

        [Test]
        public void ShouldNotAllowAddingDuplicateDevices()
        {
            persistor.AddDevice("device", "manufacturer");
            Assert.Throws(typeof(ArgumentException), () => persistor.AddDevice("device", "manufacturer"));
        }

        [Test]
        public void ShouldAddSource()
        {
            var label = "animal";
            var src = persistor.AddSource(label, null);

            Assert.AreEqual(label, src.Label);
            Assert.AreEqual(0, src.Sources.Count());
            Assert.AreEqual(0, src.EpochGroups.Count());

            CollectionAssert.AreEquivalent(new[] {src}, persistor.Experiment.Sources);
        }

        [Test]
        public void ShouldNotAddInvalidSource()
        {
            Assert.Throws(typeof(ArgumentException), () => persistor.AddSource(null, null));
            Assert.Throws(typeof(ArgumentException), () => persistor.AddSource("", null));
            Assert.AreEqual(0, persistor.Experiment.Sources.Count());
        }

        [Test]
        public void ShouldNestSources()
        {
            var top = persistor.AddSource("top", null);
            var mid1 = persistor.AddSource("mid1", top);
            var btm = persistor.AddSource("btm", mid1);
            var mid2 = persistor.AddSource("mid2", top);

            CollectionAssert.AreEquivalent(new[] {top}, persistor.Experiment.Sources);
            CollectionAssert.AreEquivalent(new[] {mid1, mid2}, top.Sources);
            CollectionAssert.AreEquivalent(new[] {btm}, mid1.Sources);
            Assert.AreEqual(0, mid2.Sources.Count());
        }

        [Test]
        public void ShouldAllowMultipleSourcesWithSameLabel()
        {
            const string label = "label";

            var s1 = persistor.AddSource(label, null);
            var s2 = persistor.AddSource(label, null);

            Assert.AreNotEqual(s1, s2);
            Assert.AreEqual(2, persistor.Experiment.Sources.Count());
        }

        [Test]
        public void ShouldNotAllowDeletingSourceWithEpochGroup()
        {
            var src1 = persistor.AddSource("source1", null);
            persistor.BeginEpochGroup("group1", src1, DateTimeOffset.Now);
            Assert.Throws(typeof (InvalidOperationException), () => persistor.Delete(src1));

            var src2 = persistor.AddSource("source2", null);
            var src3 = persistor.AddSource("source3", src2);
            persistor.BeginEpochGroup("group2", src3, DateTimeOffset.Now);
            Assert.Throws(typeof (InvalidOperationException), () => persistor.Delete(src2));
        }

        [Test]
        public void ShouldAllowDeletingSourceWithDeletedEpochGroup()
        {
            var src = persistor.AddSource("source", null);
            var grp = persistor.BeginEpochGroup("group", src, DateTimeOffset.Now);
            persistor.EndEpochGroup(DateTimeOffset.Now);

            persistor.Delete(grp);
            persistor.Delete(src);
            Assert.AreEqual(0, persistor.Experiment.Sources.Count());
        }

        [Test]
        public void ShouldBeginEpochGroup()
        {
            var label = "group";
            var time = DateTimeOffset.Now;
            var src = persistor.AddSource("label", null);
            var grp = persistor.BeginEpochGroup(label, src, time);

            Assert.AreEqual(label, grp.Label);
            Assert.AreEqual(src, grp.Source);
            Assert.AreEqual(time, grp.StartTime);
            Assert.IsNull(grp.EndTime);
            Assert.AreEqual(0, grp.EpochGroups.Count());
            Assert.AreEqual(0, grp.EpochBlocks.Count());

            CollectionAssert.AreEquivalent(new[] {grp}, persistor.Experiment.EpochGroups);
        }

        [Test]
        public void ShouldNotBeginInvalidEpochGroup()
        {
            var src = persistor.AddSource("label", null);
            Assert.Throws(typeof(ArgumentException), () => persistor.BeginEpochGroup("group", null));
            Assert.Throws(typeof(ArgumentException), () => persistor.BeginEpochGroup(null, src));
            Assert.Throws(typeof(ArgumentException), () => persistor.BeginEpochGroup("", src));
            Assert.AreEqual(0, persistor.Experiment.EpochGroups.Count());
        }

        [Test]
        public void ShouldNotAllowDeletingOpenEpochGroup()
        {
            var src = persistor.AddSource("label", null);
            var grp1 = persistor.BeginEpochGroup("group1", src);
            Assert.Throws(typeof (InvalidOperationException), () => persistor.Delete(grp1));

            var grp2 = persistor.BeginEpochGroup("group2", src);
            Assert.Throws(typeof(InvalidOperationException), () => persistor.Delete(grp1));
            Assert.Throws(typeof(InvalidOperationException), () => persistor.Delete(grp2));
        }

        [Test]
        public void ShouldAllowDeletingClosedEpochGroup()
        {
            var src = persistor.AddSource("label", null);
            var grp1 = persistor.BeginEpochGroup("group1", src);
            persistor.EndEpochGroup();
            persistor.Delete(grp1);
            Assert.AreEqual(0, persistor.Experiment.EpochGroups.Count());
        }

        [Test]
        public void ShouldSetEpochGroupEndTimeOnEnd()
        {
            var src = persistor.AddSource("label", null);
            var grp = persistor.BeginEpochGroup("group", src);

            var time = DateTimeOffset.Now;
            persistor.EndEpochGroup(time);
            Assert.AreEqual(time, grp.EndTime);
        }

        [Test]
        public void ShouldNestEpochGroups()
        {
            var src = persistor.AddSource("label", null);

            var top = persistor.BeginEpochGroup("top", src);
            var mid1 = persistor.BeginEpochGroup("mid1", src);
            var btm = persistor.BeginEpochGroup("btm", src);
            persistor.EndEpochGroup();
            persistor.EndEpochGroup();
            var mid2 = persistor.BeginEpochGroup("mid2", src);

            CollectionAssert.AreEquivalent(new[] {top}, persistor.Experiment.EpochGroups);
            CollectionAssert.AreEquivalent(new[] {mid1, mid2}, top.EpochGroups);
            CollectionAssert.AreEquivalent(new[] {btm}, mid1.EpochGroups);
            Assert.AreEqual(0, mid2.EpochGroups.Count());
        }

        [Test]
        public void ShouldBeginEpochBlock()
        {
            var id = "protocol.id.here";
            var time = DateTimeOffset.Now;
            var src = persistor.AddSource("label", null);
            var grp = persistor.BeginEpochGroup("group", src, time);
            var blk = persistor.BeginEpochBlock(id, time);

            Assert.AreEqual(id, blk.ProtocolID);
            Assert.AreEqual(time, blk.StartTime);
            Assert.IsNull(blk.EndTime);
            Assert.AreEqual(0, blk.Epochs.Count());

            CollectionAssert.AreEquivalent(new[] {blk}, grp.EpochBlocks);
        }

        [Test]
        public void ShouldNotBeginInvalidEpochBlock()
        {
            var src = persistor.AddSource("label", null);
            var grp = persistor.BeginEpochGroup("group", src);
            Assert.Throws(typeof(ArgumentException), () => persistor.BeginEpochBlock(null, DateTimeOffset.Now));
            Assert.Throws(typeof(ArgumentException), () => persistor.BeginEpochBlock("", DateTimeOffset.Now));
            Assert.AreEqual(0, grp.EpochBlocks.Count());
        }

        [Test]
        public void ShouldAllowOnlyOneOpenEpochBlock()
        {
            var src = persistor.AddSource("label", null);
            var grp = persistor.BeginEpochGroup("group", src);
            var blk = persistor.BeginEpochBlock("id", DateTimeOffset.Now);

            Assert.Throws(typeof (InvalidOperationException),
                          () => persistor.BeginEpochBlock("id", DateTimeOffset.Now));
        }

        [Test]
        public void ShouldSetEpochBlockEndTimeOnEnd()
        {
            var src = persistor.AddSource("label", null);
            var grp = persistor.BeginEpochGroup("group", src);
            var blk = persistor.BeginEpochBlock("id", DateTimeOffset.Now);

            var time = DateTimeOffset.Now;
            persistor.EndEpochBlock(time);
            Assert.AreEqual(time, blk.EndTime);
        }

        [Test]
        public void ShouldSerializeEpoch()
        {
            ExternalDeviceBase dev1;
            ExternalDeviceBase dev2;
            var epoch = CreateTestEpoch(out dev1, out dev2);

            var src = persistor.AddSource("label", null);
            var grp = persistor.BeginEpochGroup("group", src);
            var blk = persistor.BeginEpochBlock(epoch.ProtocolID, epoch.StartTime);
            
            var e = persistor.Serialize(epoch);

            Assert.AreEqual((DateTimeOffset) epoch.StartTime, e.StartTime);
            Assert.AreEqual((DateTimeOffset) epoch.StartTime + epoch.Duration, e.EndTime);
            CollectionAssert.AreEquivalent(epoch.ProtocolParameters, e.ProtocolParameters);
            CollectionAssert.AreEquivalent(epoch.Keywords, e.Keywords);

            // Backgrounds
            Assert.AreEqual(epoch.Backgrounds.Count, e.Backgrounds.Count());
            foreach (var kv in epoch.Backgrounds)
            {
                var device = kv.Key;
                var background = kv.Value;

                var bk = e.Backgrounds.First(b => b.Device.Name == device.Name && b.Device.Manufacturer == device.Manufacturer);
                
                Assert.AreEqual(background.Value, bk.Value);
                Assert.AreEqual(background.SampleRate, bk.SampleRate);
            }

            // Stimuli
            Assert.AreEqual(epoch.Stimuli.Count, e.Stimuli.Count());

            var s1 = e.Stimuli.First(s => s.Device.Name == dev1.Name);

            Assert.AreEqual(epoch.Stimuli[dev1].StimulusID, s1.StimulusID);
            Assert.AreEqual(epoch.Stimuli[dev1].Units, s1.Units);
            CollectionAssert.AreEquivalent(epoch.Stimuli[dev1].Parameters, s1.Parameters);
            AssertConfigurationSpansEqual(epoch.Stimuli[dev1].OutputConfigurationSpans, s1.ConfigurationSpans);

            var s2 = e.Stimuli.First(s => s.Device.Name == dev2.Name);

            Assert.AreEqual(epoch.Stimuli[dev2].StimulusID, s2.StimulusID);
            Assert.AreEqual(epoch.Stimuli[dev2].Units, s2.Units);
            CollectionAssert.AreEquivalent(epoch.Stimuli[dev2].Parameters, s2.Parameters);
            AssertConfigurationSpansEqual(epoch.Stimuli[dev2].OutputConfigurationSpans, s2.ConfigurationSpans);

            // Responses
            Assert.AreEqual(epoch.Responses.Count, e.Responses.Count());

            var r1 = e.Responses.First(r => r.Device.Name == dev1.Name);

            Assert.AreEqual(epoch.Responses[dev1].SampleRate, r1.SampleRate);
            CollectionAssert.AreEqual(epoch.Responses[dev1].Data, r1.Data);
            AssertConfigurationSpansEqual(epoch.Responses[dev1].DataConfigurationSpans, r1.ConfigurationSpans);

            var r2 = e.Responses.First(r => r.Device.Name == dev2.Name);

            Assert.AreEqual(epoch.Responses[dev2].SampleRate, r2.SampleRate);
            CollectionAssert.AreEqual(epoch.Responses[dev2].Data, r2.Data);
            AssertConfigurationSpansEqual(epoch.Responses[dev2].DataConfigurationSpans, r2.ConfigurationSpans);
        }

        [Test]
        public void ShouldNotAllowSerializingEpochToBlockWithDifferentProtocolId()
        {
            ExternalDeviceBase dev1;
            ExternalDeviceBase dev2;
            var epoch = CreateTestEpoch(out dev1, out dev2);

            var src = persistor.AddSource("label", null);
            var grp = persistor.BeginEpochGroup("group", src);
            var blk = persistor.BeginEpochBlock("different.protocol.id", epoch.StartTime);

            Assert.Throws(typeof (ArgumentException), () => persistor.Serialize(epoch));
        }

        [Test]
        public void ShouldNotAllowSerializingEpochWithoutOpenEpochBlock()
        {
            ExternalDeviceBase dev1;
            ExternalDeviceBase dev2;
            var epoch = CreateTestEpoch(out dev1, out dev2);

            Assert.Throws(typeof (InvalidOperationException), () => persistor.Serialize(epoch));

            var src = persistor.AddSource("label", null);
            var grp = persistor.BeginEpochGroup("group", src);
            var blk = persistor.BeginEpochBlock(epoch.ProtocolID, DateTimeOffset.Now);
            persistor.EndEpochBlock(DateTimeOffset.Now);

            Assert.Throws(typeof(InvalidOperationException), () => persistor.Serialize(epoch));
        }

        [Test]
        public void ShouldSetEndTimeOnAllOpenEntitiesOnClose()
        {
            var startTime = DateTimeOffset.Now;
            var src = persistor.AddSource("label", null);
            var grp1 = persistor.BeginEpochGroup("group1", src);
            var grp2 = persistor.BeginEpochGroup("group2", src);
            var blk = persistor.BeginEpochBlock("id", startTime);

            var endTime = startTime.AddHours(1).AddMinutes(2).AddSeconds(55);
            persistor.Close(endTime);

            persistor = new H5EpochPersistor(TEST_FILE);
            var exp = persistor.Experiment;
            grp1 = exp.EpochGroups.First();
            grp2 = grp1.EpochGroups.First();
            blk = grp2.EpochBlocks.First();

            Assert.AreEqual(endTime, exp.EndTime);
            Assert.AreEqual(endTime, grp1.EndTime);
            Assert.AreEqual(endTime, grp2.EndTime);
            Assert.AreEqual(endTime, blk.EndTime);
        }

        private static void AssertConfigurationSpansEqual(IEnumerable<IConfigurationSpan> expected, IEnumerable<IConfigurationSpan> actual)
        {
            var expectedSpans = expected.ToList();
            var actualSpans = actual.ToList();
            Assert.AreEqual(expectedSpans.Count, actualSpans.Count);

            for (int i = 0; i < expectedSpans.Count; i++)
            {
                Assert.AreEqual(expectedSpans[i].Time, actualSpans[i].Time);

                var expectedNodes = expectedSpans[i].Nodes.ToList();
                var actualNodes = actualSpans[i].Nodes.ToList();
                Assert.AreEqual(expectedNodes.Count, actualNodes.Count);

                for (int j = 0; j < expectedNodes.Count; j++)
                {
                    Assert.AreEqual(expectedNodes[j].Name, actualNodes[j].Name);
                    CollectionAssert.AreEquivalent(expectedNodes[j].Configuration, actualNodes[j].Configuration);
                }
            }
        }

        private static Epoch CreateTestEpoch(out ExternalDeviceBase dev1, out ExternalDeviceBase dev2)
        {
            dev1 = new UnitConvertingExternalDevice("dev1", "man1", new Measurement(0, "V"));
            dev2 = new UnitConvertingExternalDevice("dev2", "man2", new Measurement(0, "V"));

            var stream1 = new DAQInputStream("Stream1");
            var stream2 = new DAQInputStream("Stream2");

            var stimParameters = new Dictionary<string, object>();
            stimParameters["param1"] = 1;
            stimParameters["param2"] = 2;

            var srate = new Measurement(1000, "Hz");

            List<Measurement> samples =
                Enumerable.Range(0, 10000).Select(i => new Measurement((decimal)Math.Sin((double)i / 100), "V")).ToList();
            var stimData = new OutputData(samples, srate, false);

            var stim1 = new RenderedStimulus("RenderedStimulus", stimParameters, stimData);
            var stim2 = new RenderedStimulus("RenderedStimulus", stimParameters, stimData);

            var protocolParameters = new Dictionary<string, object>
                {
                    {"one", 1},
                    {"two", "second"},
                    {"three", 5.55}
                };

            var epoch = new TestEpoch("protocol.banana", protocolParameters);
            epoch.Stimuli[dev1] = stim1;
            epoch.Stimuli[dev2] = stim2;

            DateTimeOffset start = DateTimeOffset.Now;
            epoch.SetStartTime(Maybe<DateTimeOffset>.Yes(start));

            epoch.Backgrounds[dev1] = new Background(new Measurement(0, "V"), new Measurement(1000, "Hz"));
            epoch.Backgrounds[dev2] = new Background(new Measurement(1, "V"), new Measurement(1000, "Hz"));

            epoch.Responses[dev1] = new Response();
            epoch.Responses[dev2] = new Response();

            var streamConfig = new Dictionary<string, object>();
            streamConfig["configParam1"] = 1;

            var devConfig = new Dictionary<string, object>();
            devConfig["configParam2"] = 2;

            IInputData responseData1 = new InputData(samples, srate, start)
                .DataWithStreamConfiguration(stream1, streamConfig)
                .DataWithExternalDeviceConfiguration(dev1, devConfig);
            IInputData responseData2 = new InputData(samples, srate, start)
                .DataWithStreamConfiguration(stream2, streamConfig)
                .DataWithExternalDeviceConfiguration(dev2, devConfig);

            epoch.Responses[dev1].AppendData(responseData1);
            epoch.Responses[dev1].AppendData(responseData2);
            epoch.Responses[dev2].AppendData(responseData2);

            epoch.Keywords.Add("word1");
            epoch.Keywords.Add("word2");

            return epoch;
        }

        private class TestEpoch : Epoch
        {
            public TestEpoch(string protocolID, IDictionary<string, object> parameters)
                : base(protocolID, parameters)
            {
            }

            public override Maybe<DateTimeOffset> StartTime { get { return _startTime; } }

            private Maybe<DateTimeOffset> _startTime;

            public void SetStartTime(Maybe<DateTimeOffset> t)
            {
                _startTime = t;
            }
        }
    }
}
