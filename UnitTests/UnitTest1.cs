using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Hpc.Scheduler.Communicator;

namespace UnitTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var folder = @"C:\Users\qiufangs\Documents\GitHub\whpc-linux-communicator\LinuxCommunicator\bin\Debug\";

            foreach (var fileName in Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly))
            {
                Assembly assembly;

                try
                {
                    assembly = Assembly.LoadFrom(fileName);
                }
                catch (FileLoadException ex)
                {
                    Console.WriteLine(string.Format("Unable to load file {0}. Exception {1}", fileName, ex));
                    continue;
                }

                IEnumerable<Type> types = null;
                try
                {
                    types = assembly.GetTypes();
                    types = types.Where(t => t.GetInterface("IUnmanagedResourceCommunicator") == typeof(IUnmanagedResourceCommunicator));
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Console.WriteLine(string.Format("Unable to load types. Exception {0}", ex));
                    foreach (var e in ex.LoaderExceptions)
                    {
                        Console.WriteLine(string.Format("Ex {0}", e));
                    }
                }

                if (types != null)
                {
                    foreach (var communicatorType in types)
                    {
                        IUnmanagedResourceCommunicator instance;
                        try
                        {
                            instance = Activator.CreateInstance(communicatorType) as IUnmanagedResourceCommunicator;
                            if (instance == null)
                            {
                                throw new InvalidProgramException();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(string.Format("Unable to initialize the type {0}. Exception {1}", communicatorType.Name, ex));
                            throw;
                        }

                        instance.StartJobAndTask("", null, "", "", null, null);
                        Console.WriteLine(String.Format("Successful loading {0}", instance));
                    }
                }
            }
        }
    }
}
