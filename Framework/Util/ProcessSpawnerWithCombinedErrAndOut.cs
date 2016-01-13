﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;

namespace Test.Framework.Util {
	/// <summary>
	/// Executes an <see cref="System.Diagnostics.Process" /> object in order to get sequentially consistent output for assertion purposes. 
	/// <see cref="System.Diagnostics.Process" /> has a limitation where sequentially consistent output (as you would see in a regular console execution) and distinct output cannot be obtained at the same time and it may be a limitation of Windows itself.
	/// If both outputs are required, please use <see cref="Test.Framework.Util.ProcessSpawnerWithCombinedAndSplitErrAndOut" />.
	/// </summary>
	internal sealed class ProcessSpawnerWithCombinedErrAndOut : IProcessSpawner {
		private Process m_process = null;
		private Process m_child = null;
		private StringBuilder m_combinedOutput = new StringBuilder(2048);
		private StringBuilder m_outputSinceLastInput = null;
		private long m_procPeakPagedMemorySize;
		private long m_procPeakVirtualMemorySize;
		private long m_procPeakWorkingSet;

		/// <summary>
		/// Indicates that the process has started.
		/// </summary>
		public bool Started { get; private set; }
		/// <summary>
		/// Indicates that the process has completed.
		/// </summary>
		public bool Exited { get; private set; }
		/// <summary>
		/// Is called when the process is asking for input.
		/// Frequently, this will be called with a buffer of string.Empty, output from the program, or output based on user input.
		/// All of these states should be accounted for.
		/// </summary>
		public event ProcessInputDelegate OnInputRequested;

		/// <summary>
		/// Initializes a new instance of the <see cref="Test.Framework.Util.ProcessSpawnerWithCombinedErrAndOut" /> class with a specified file.
		/// </summary>
		/// <param name="file">The name of the file to execute.</param>
		public ProcessSpawnerWithCombinedErrAndOut(string file) { Initialize(file, null, null); }
		/// <summary>
		/// Initializes a new instance of the <see cref="Test.Framework.Util.ProcessSpawnerWithCombinedErrAndOut" /> class with a specified file and command line arguments.
		/// </summary>
		/// <param name="file">The name of the file to execute.</param>
		/// <param name="args">The command line arguments to pass to the execution of the file.</param>
		public ProcessSpawnerWithCombinedErrAndOut(string file, params object[] args) { Initialize(file, new WindowsCommandLineArgumentEscaper(), args); }
		/// <summary>
		/// Initializes a new instance of the <see cref="Test.Framework.Util.ProcessSpawnerWithCombinedErrAndOut" /> class with a specified file, command line escaper, and command line arguments.
		/// </summary>
		/// <param name="file">The name of the file to execute.</param>
		/// <param name="escaper">The command line escaper to produce a command line argument string from the command line arguments.</param>
		/// <param name="args">The command line arguments to pass to the execution of the file.</param>
		public ProcessSpawnerWithCombinedErrAndOut(string file, ICommandLineArgumentEscaper escaper, params object[] args) { Initialize(file, escaper, args); }
		/// <summary>
		/// Initializes a new instance of the <see cref="Test.Framework.Util.ProcessSpawnerWithCombinedErrAndOut" /> class with a specified file.
		/// </summary>
		/// <param name="file">The <see cref="FileInfo" /> carrying information about the file to execute.</param>
		public ProcessSpawnerWithCombinedErrAndOut(FileInfo file) { Initialize(file, null, null); }
		/// <summary>
		/// Initializes a new instance of the <see cref="Test.Framework.Util.ProcessSpawnerWithCombinedErrAndOut" /> class with a specified file and command line arguments.
		/// </summary>
		/// <param name="file">The <see cref="FileInfo" /> carrying information about the file to execute.</param>
		/// <param name="args">The command line arguments to pass to the execution of the file.</param>
		public ProcessSpawnerWithCombinedErrAndOut(FileInfo file, params object[] args) { Initialize(file, new WindowsCommandLineArgumentEscaper(), args); }
		/// <summary>
		/// Initializes a new instance of the <see cref="Test.Framework.Util.ProcessSpawnerWithCombinedErrAndOut" /> class with a specified file, command line escaper, and command line arguments.
		/// </summary>
		/// <param name="file">The <see cref="FileInfo" /> carrying information about the file to execute.</param>
		/// <param name="escaper">The command line escaper to produce a command line argument string from the command line arguments.</param>
		/// <param name="args">The command line arguments to pass to the execution of the file.</param>
		public ProcessSpawnerWithCombinedErrAndOut(FileInfo file, ICommandLineArgumentEscaper escaper, params object[] args) { Initialize(file, escaper, args); }

		private void Initialize(string file, ICommandLineArgumentEscaper escaper, params object[] args) {
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (file.Length == 0) throw new ArgumentException("String is blank", nameof(file));
			Initialize(new FileInfo(file), escaper, args);
		}

		private void Initialize(FileInfo file, ICommandLineArgumentEscaper escaper, params object[] args) {
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (!file.Exists) throw new FileNotFoundException("File not found", file.FullName);
			if (args != null && args.Any()) {
				if (escaper == null) throw new ArgumentNullException(nameof(escaper));
			}

			var startInfo = new ProcessStartInfo() {
				FileName = "cmd.exe",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			string arguments = string.Empty;
			if (args != null && args.Any()) {
				arguments = escaper.Escape(args);
			}

			// Needed for Windows again
			string quote = @"""";
			startInfo.Arguments = "/c " + quote + file.FullName + quote + " " + arguments + " 2>&1";

			m_process = new Process() {
				StartInfo = startInfo,
				EnableRaisingEvents = true,
			};

			m_process.OutputDataReceived += (sender, e) => {
				if (e.Data == null) return;
				if (m_outputSinceLastInput != null) {
					if (m_outputSinceLastInput.Length != 0) m_outputSinceLastInput.AppendLine();
					m_outputSinceLastInput.Append(e.Data);
				}
				if (m_combinedOutput.Length != 0) m_combinedOutput.AppendLine();
				m_combinedOutput.Append(e.Data);
			};
		}

		private ProcessResult ProduceResult() {
			return new ProcessResult(
				stdOutput: null,
				errorOutput: null,
				fullOutput: m_combinedOutput.ToString(),
				exitCode: m_process.ExitCode,
				startTime: m_process.StartTime,
				exitTime: m_process.ExitTime,
				privilegedProcessorTime: m_process.PrivilegedProcessorTime,
				userProcessorTime: m_process.UserProcessorTime,
				totalProcessorTime: m_process.TotalProcessorTime,
				peakPagedMemorySize: m_procPeakPagedMemorySize,
				peakVirtualMemorySize: m_procPeakVirtualMemorySize,
				peakWorkingSet: m_procPeakWorkingSet
			);
		}

		/// <summary>
		/// Executes the specified process.
		/// </summary>
		/// <returns>A <see cref="Test.Framework.Util.ProcessResult" /> object representing the execution.</returns>
		public ProcessResult Run() {
			return WaitForExit();
		}

		private void Start() {
			if (Exited) throw new InvalidOperationException("Must not execute the process twice");
			if (Started) throw new InvalidOperationException("Must not execute the process twice");
			if (OnInputRequested != null) {
				m_outputSinceLastInput = new StringBuilder(1024);
			}

			Started = true;
			m_process.Start();
			m_process.BeginOutputReadLine();
		}

		private ProcessResult WaitForExit() {
			if (Exited) throw new InvalidOperationException("Must not execute the process twice");
			if (!Started) Start();

			while (true) {
				if (!m_process.HasExited) {
					try {
						m_procPeakPagedMemorySize = m_process.PeakPagedMemorySize64;
						m_procPeakVirtualMemorySize = m_process.PeakVirtualMemorySize64;
						m_procPeakWorkingSet = m_process.PeakWorkingSet64;

						if (OnInputRequested != null) {
							if (m_child == null) {
								try {
									using (var mos = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + m_process.Id)) {
										using (ManagementObjectCollection collection = mos.Get()) {
											foreach (ManagementObject mo in collection) {
												if (m_child != null) {
													throw new InvalidOperationException("Unexpected number of child processes");
												}
												m_child = Process.GetProcessById(Convert.ToInt32(mo["ProcessID"]));
												mo.Dispose();
											}
										}
									}
								}
								catch (ArgumentException) {
									// Process not running
									// This is okay because it means there is no input to capture and it was a short program
								}
							}

							if (m_child != null) {
								foreach (ProcessThread thread in m_child.Threads) {
									if (thread.ThreadState == ThreadState.Wait && thread.WaitReason == ThreadWaitReason.UserRequest) {
										ProcessInputHandleResult result = OnInputRequested(m_outputSinceLastInput.ToString(), m_process.StandardInput);
										if (result == ProcessInputHandleResult.Handled) {
											m_outputSinceLastInput.Clear();
										}
										break;
									}
								}
							}
						}

						m_process.Refresh();
					}
					catch (InvalidOperationException) { break; }
				}
				else break;
			}

			// Allow any outstanding events to finish
			m_process.WaitForExit();

			Exited = true;
			return ProduceResult();
		}

		/// <summary>
		/// Releases all resources used by the current instance of the <see cref="Test.Framework.Util.ProcessSpawnerWithCombinedErrAndOut" /> class.
		/// </summary>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if (disposing) {
				m_process.Dispose();
				if (m_child != null) m_child.Dispose();
			}
		}
	}
}
