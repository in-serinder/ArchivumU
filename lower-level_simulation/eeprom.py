#!/usr/bin/env python3
# EEPROM模拟模块
# 模拟两片24C64 EEPROM，每片8KB

import os

class EEPROM:
    """单块24C64 EEPROM模拟"""
    SIZE = 8192  # 8KB
    
    def __init__(self, device_addr, filename):
        self.device_addr = device_addr
        self.filename = filename
        self.data = bytearray([0x00] * self.SIZE)
        self.load_from_file()
    
    def write(self, addr, data):
        """写入数据到指定地址"""
        for i, byte in enumerate(data):
            if addr + i < self.SIZE:
                self.data[addr + i] = byte
    
    def read(self, addr, length):
        """从指定地址读取数据"""
        if addr + length > self.SIZE:
            length = self.SIZE - addr
        return bytes(self.data[addr:addr + length])
    
    def format(self):
        """格式化EEPROM，全部写入0x00"""
        self.data = bytearray([0x00] * self.SIZE)
    
    def load_from_file(self):
        """从文件加载数据"""
        if os.path.exists(self.filename):
            with open(self.filename, 'rb') as f:
                content = f.read(self.SIZE)
                self.data[:len(content)] = content
    
    def save_to_file(self):
        """保存数据到文件"""
        with open(self.filename, 'wb') as f:
            f.write(self.data)

class EEPROMManager:
    """两片24C64 EEPROM管理器"""
    
    def __init__(self):
        self.ic0 = EEPROM(0x50, '24c64_sim_c1.bin')  # IC_0_24C64
        self.ic1 = EEPROM(0x51, '24c64_sim_c2.bin')  # IC_1_24C64
    
    def write(self, addr_12bit, data):
        """写入数据，12位地址，最高位区分芯片"""
        chip = addr_12bit >> 11  # 最高位选择芯片
        addr = addr_12bit & 0x7FF  # 低11位为片内地址
        
        if chip == 0:
            self.ic0.write(addr, data)
        else:
            self.ic1.write(addr, data)
    
    def read(self, addr_12bit, length):
        """读取数据，12位地址"""
        chip = addr_12bit >> 11
        addr = addr_12bit & 0x7FF
        
        if chip == 0:
            return self.ic0.read(addr, length)
        else:
            return self.ic1.read(addr, length)
    
    def format(self):
        """格式化所有EEPROM"""
        self.ic0.format()
        self.ic1.format()
    
    def save_all(self):
        """保存所有EEPROM数据到文件"""
        self.ic0.save_to_file()
        self.ic1.save_to_file()