// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace TMD2772
{
	struct ProxALS
	{
		public double PROX;
		public double ALS;
	};

	// App that reads data over I2C from an TMD2772 Proximity and Light Sensor
	public sealed partial class MainPage : Page
	{
		private const byte PROXALS_I2C_ADDR = 0x39;		// I2C address of the TMD2772
		private const byte PROXALS_REG_ENABLE = 0x80;		// Enables state and interrupt register
		private const byte PROXALS_REG_ATIME = 0x81;        	// RGBC integration time register
        	private const byte PROXALS_REG_PTIME = 0x82;		// PROXALSimity ADC time register
		private const byte PROXALS_REG_WTIME = 0x83;		// Wait time register
		private const byte PROXALS_REG_CONTROL = 0x8F;		// Control register
		private const byte PROXALS_REG_C0DATA = 0x94;		// ALS Ch0 ADC low data register
		private const byte PROXALS_REG_C1DATA = 0x96;		// ALS Ch1 ADC low data register
		private const byte PROXALS_REG_PDATA = 0x98;		// Proximity ADC low data register

		private I2cDevice I2CProxALS;
		private Timer periodicTimer;

		public MainPage()
		{
			this.InitializeComponent();

			// Register for the unloaded event so we can clean up upon exit
			Unloaded += MainPage_Unloaded;

			// Initialize the I2C bus, Proximity and Light Sensor, and timer
			InitI2CProxALS();
		}

		private async void InitI2CProxALS()
		{
			string aqs = I2cDevice.GetDeviceSelector();		// Get a selector string that will return all I2C controllers on the system
			var dis = await DeviceInformation.FindAllAsync(aqs);	// Find the I2C bus controller device with our selector string
			if (dis.Count == 0)
			{
				Text_Status.Text = "No I2C controllers were found on the system";
				return;
			}

			var settings = new I2cConnectionSettings(PROXALS_I2C_ADDR);
			settings.BusSpeed = I2cBusSpeed.FastMode;
			I2CProxALS = await I2cDevice.FromIdAsync(dis[0].Id, settings);	// Create an I2C Device with our selected bus controller and I2C settings
			if (I2CProxALS == null)
			{
				Text_Status.Text = string.Format(
					"Slave address {0} on I2C Controller {1} is currently in use by " +
					"another application. Please ensure that no other applications are using I2C.",
				settings.SlaveAddress,
				dis[0].Id);
				return;
			}

			/*
				Initialize the Proximity and Light Sensor:
				For this device, we create 2-byte write buffers:
				The first byte is the register address we want to write to.
				The second byte is the contents that we want to write to the register.
			*/
			byte[] WriteBuf_Enable = new byte[] { PROXALS_REG_ENABLE, 0x0F };		// 0x03 sets Power ON and Wait, Proximity and ALS features are enabled
			byte[] WriteBuf_Atime = new byte[] { PROXALS_REG_ATIME, 0xFF };			// 0x00 sets ATIME : 2.73 ms, 1 cycle, 1024 max count
			byte[] WriteBuf_Ptime = new byte[] { PROXALS_REG_PTIME, 0xFF };			// 0x00 sets PTIME : 2.73 ms, 1 cycle, 1023 max count
			byte[] WriteBuf_Wtime = new byte[] { PROXALS_REG_WTIME, 0xFF };			// 0xFF sets WTIME : 2.73 ms (WLONG = 0), 1 wait time
			byte[] WriteBuf_Control = new byte[] { PROXALS_REG_CONTROL, 0x20 };		// 0x20 sets 120 mA LED strength, Proximity uses CH1 diode, Proximity gain 1x, ALS gain 1x

			// Write the register settings
			try
			{
				I2CProxALS.Write(WriteBuf_Enable);
				I2CProxALS.Write(WriteBuf_Atime);
				I2CProxALS.Write(WriteBuf_Ptime);
				I2CProxALS.Write(WriteBuf_Wtime);
				I2CProxALS.Write(WriteBuf_Control);
			}
			// If the write fails display the error and stop running
			catch (Exception ex)
			{
				Text_Status.Text = "Failed to communicate with device: " + ex.Message;
				return;
			}

			// Create a timer to read data every 300ms
			periodicTimer = new Timer(this.TimerCallback, null, 0, 300);
		}

		private void MainPage_Unloaded(object sender, object args)
		{
			// Cleanup
			I2CProxALS.Dispose();
		}

		private void TimerCallback(object state)
		{
			string alsText, proxText;
			string addressText, statusText;

			// Read and format Proximity and Light Sensor data
			try
			{
				ProxALS proxals = ReadI2CProxALS();
				addressText = "I2C Address of the Proximity and Light Sensor TMD2772: 0x39";
				alsText = String.Format("Ambient Light Luminance: {0:F0} lux", proxals.ALS);
				proxText = String.Format("Proximity of the Device: {0:F0}", proxals.PROX);
				statusText = "Status: Running";
			}
			catch (Exception ex)
			{
				alsText = "Ambient Light Luminance: Error";
				proxText = "Proximity of the Device: Error";
				statusText = "Failed to read from Proximity and Light Sensor: " + ex.Message;
			}

			// UI updates must be invoked on the UI thread
			var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
			{
				Text_Ambient_Light_Luminance.Text = alsText;
				Text_Proximity_of_the_Device.Text = proxText;
				Text_Status.Text = statusText;
			});
		}

		private ProxALS ReadI2CProxALS()
		{
			byte[] RegAddrBuf = new byte[] { PROXALS_REG_C0DATA };	// Read data from the register address
			byte[] ReadBuf = new byte[6];				// We read 6 bytes sequentially to get all 3 two-byte data registers in one read

			/*
				Read from the Proximity and Light Sensor 
				We call WriteRead() so we first write the address of the ALS CH0 data low register, then read all 3 values
			*/
			I2CProxALS.WriteRead(RegAddrBuf, ReadBuf);

			/*
				In order to get the raw 16-bit data values, we need to concatenate two 8-bit bytes from the I2C read.
			*/
			
			ushort c0Data = (ushort)(ReadBuf[0] & 0xFF);
			c0Data |= (ushort)((ReadBuf[1] & 0xFF) * 256);
			ushort c1Data = (ushort)(ReadBuf[2] & 0xFF);
			c1Data |= (ushort)((ReadBuf[3] & 0xFF) * 256);
			ushort proximity = (ushort)(ReadBuf[4] & 0xFF);
			proximity |= (ushort)((ReadBuf[5] & 0xFF) * 256);
			double luminance = 0.0;
			double CPL = 2.73 / 20;
			double luminance1 = (1.00 *  c0Data - (1.75 * c1Data)) / CPL;
			double luminance2 = ((0.63 * c0Data) - (1.00 * c1Data)) / CPL;
			if (luminance1 > 0 && luminance2 > 0)
			{
				if (luminance1 > luminance2)
				{
					luminance = luminance1;
				}
				else
				{
					luminance = luminance2;
				}
			}

			ProxALS proxals;
			proxals.ALS = luminance;
			proxals.PROX = proximity;

			return proxals;
		}
	}
}
