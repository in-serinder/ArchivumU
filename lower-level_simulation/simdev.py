# 模拟设备发送串口消息和响应
import serial

'''
两个本地存储用 当AT+INIT 时创建这三个二进制文件
stc8geeprom.bin(4k主控内配置区域) 24c64_sim_c1.bin(8k第一片) 24c64_sim_c2.bin(8k第二片) 
分别存储片内配置和24c64 EEPROM内存储区 
'''

