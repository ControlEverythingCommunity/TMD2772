# Distributed with a free-will license.
# Use it any way you want, profit or free, provided it fits in the licenses of its associated works.
# TMD2772
# This code is designed to work with the TMD2772_I2CS I2C Mini Module available from ControlEverything.com.
# https://www.controleverything.com/content/Light?sku=TMD2772_I2CS#tabs-0-product_tabset-2

import smbus
import time

# Get I2C bus
bus = smbus.SMBus(1)

# TMD2772 address, 0x39(57)
# Select enable register 0x00(00), with command register, 0x80(128)
#		0x0F(15)	Power ON, Wait enable, Proximity enable, ALS enable
bus.write_byte_data(0x39, 0x00 | 0x80, 0x0F)
# TMD2772 address, 0x39(57)
# Select ALS time register 0x01(01), with command register, 0x80(128)
#		0xFF(255)	ATime - 2.73ms
bus.write_byte_data(0x39, 0x01 | 0x80, 0xFF)
# TMD2772 address, 0x39(57)
# Select proximity ADC time register 0x02(02), with command register, 0x80(128)
#		0xFF(255)	PTime - 2.73ms
bus.write_byte_data(0x39, 0x02 | 0x80, 0xFF)
# TMD2772 address, 0x39(57)
# Select wait time register 0x03(03), with command register, 0x80(128)
#		0xFF(255)	WTime - 2.73ms
bus.write_byte_data(0x39, 0x03 | 0x80, 0xFF)
# TMD2772 address, 0x39(57)
# Select control register 0x0F(15), with command register, 0x80(128)
#		0x20(32)	120 mA LED strength, Proximity uses CH1 diode
#					Proximity gain 1x, ALS gain 1x
bus.write_byte_data(0x39, 0x0F | 0x80, 0x20)

time.sleep(0.5)

# TMD2772 address, 0x39(57)
# Read data back from 0x14(20), with command register, 0x80(128), 6 bytes
# c0Data LSB, c0Data MSB, c1Data LSB, c1Data MSB, Proximity LSB, Proximity MSB
data = bus.read_i2c_block_data(0x39, 0x14 | 0x80, 6)

# Convert the data
c0Data = data[1] * 256 + data[0]
c1Data = data[3] * 256 + data[2]
proximity = data[5] * 256 + data[4]
luminance = 0.0
CPL = 2.73 / 20.0
luminance1 = (1.00 *  c0Data - (1.75 * c1Data)) / CPL
luminance2 = ((0.63 * c0Data) - (1.00 * c1Data)) / CPL
if luminance1 > 0 and luminance2 > 0 :
	if luminance1 > luminance2 :
		luminance = luminance1
	else :
		luminance = luminance2

# Output data to screen
print "Ambient Light luminance : %.2f lux" %luminance
print "Proximity of the Device : %.2f" %proximity
