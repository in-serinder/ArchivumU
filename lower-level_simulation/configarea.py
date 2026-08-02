#!/usr/bin/env python3
# 片内配置区模拟模块

import os

class ConfigArea:
    """片内配置区模拟"""
    
    # 地址定义
    ADDR_CHECKSUM = 0x00
    ADDR_STATUS = 0x01
    ADDR_PASSWORD_HASH = 0x02
    ADDR_ACCESS_COUNT = 0x22
    ADDR_RESERVED = 0x24
    ADDR_DEVICE_NAME = 0x3C
    ADDR_ENCRYPTION = 0x4E
    ADDR_FREE_EXTEND = 0x4F
    
    # 状态标志位
    FLAG_INITIALIZED = 0x01
    FLAG_READ_ONLY = 0x02
    FLAG_READ_LOCKED = 0x04
    FLAG_PWD_AUTH = 0x08
    FLAG_ENCRYPTION = 0x10
    
    # 加密方式位
    ENCRYPT_AES128 = 0x01
    ENCRYPT_XOR = 0x02
    ENCRYPT_CAESAR = 0x04
    ENCRYPT_RC4 = 0x08
    
    SIZE = 4096  # 片内EEPROM大小为4KB
    
    def __init__(self):
        self.data = bytearray([0x00] * self.SIZE)  # 模拟4096字节片内EEPROM
        self.load_from_file()
        
    def load_from_file(self):
        """从文件加载配置"""
        if os.path.exists('stc8geeprom.bin'):
            with open('stc8geeprom.bin', 'rb') as f:
                content = f.read(self.SIZE)
                self.data[:len(content)] = content
        
    def save_to_file(self):
        """保存配置到文件"""
        with open('stc8geeprom.bin', 'wb') as f:
            f.write(self.data)
        
    def init_default(self):
        """初始化默认配置"""
        self.data[ConfigArea.ADDR_STATUS] = 0x00  # 未初始化
        self.data[ConfigArea.ADDR_ACCESS_COUNT:ConfigArea.ADDR_ACCESS_COUNT+2] = b'\x00\x00'
        self.data[ConfigArea.ADDR_PASSWORD_HASH:ConfigArea.ADDR_PASSWORD_HASH+32] = b'\x00' * 32
        self.data[ConfigArea.ADDR_DEVICE_NAME:ConfigArea.ADDR_DEVICE_NAME+18] = b'\x00' * 18
        self.data[ConfigArea.ADDR_ENCRYPTION] = 0x00
        
    def set_initialized(self):
        """设置设备已初始化标志"""
        self.data[ConfigArea.ADDR_STATUS] |= ConfigArea.FLAG_INITIALIZED
        
    def is_initialized(self):
        """检查设备是否已初始化"""
        return (self.data[ConfigArea.ADDR_STATUS] & ConfigArea.FLAG_INITIALIZED) != 0
    
    def set_pwd_auth_enabled(self, enabled):
        """设置密码验证功能"""
        if enabled:
            self.data[ConfigArea.ADDR_STATUS] |= ConfigArea.FLAG_PWD_AUTH
        else:
            self.data[ConfigArea.ADDR_STATUS] &= ~ConfigArea.FLAG_PWD_AUTH
    
    def is_pwd_auth_enabled(self):
        """检查密码验证是否启用"""
        return (self.data[ConfigArea.ADDR_STATUS] & ConfigArea.FLAG_PWD_AUTH) != 0
    
    def set_password_hash(self, hash_value):
        """设置密码哈希值"""
        self.data[ConfigArea.ADDR_PASSWORD_HASH:ConfigArea.ADDR_PASSWORD_HASH+32] = hash_value[:32]
    
    def get_password_hash(self):
        """获取密码哈希值"""
        return bytes(self.data[ConfigArea.ADDR_PASSWORD_HASH:ConfigArea.ADDR_PASSWORD_HASH+32])
    
    def set_device_name(self, name):
        """设置设备名称"""
        name_bytes = name.encode('utf-8')[:18]
        self.data[ConfigArea.ADDR_DEVICE_NAME:ConfigArea.ADDR_DEVICE_NAME+18] = name_bytes + b'\x00' * (18 - len(name_bytes))
    
    def get_device_name(self):
        """获取设备名称"""
        end = self.data.find(b'\x00', ConfigArea.ADDR_DEVICE_NAME)
        if end == -1:
            end = ConfigArea.ADDR_DEVICE_NAME + 18
        return bytes(self.data[ConfigArea.ADDR_DEVICE_NAME:end]).decode('utf-8')
    
    def increment_access_count(self):
        """增加接入计数"""
        current = int.from_bytes(self.data[ConfigArea.ADDR_ACCESS_COUNT:ConfigArea.ADDR_ACCESS_COUNT+2], 'big')
        if current < 65535:
            current += 1
            self.data[ConfigArea.ADDR_ACCESS_COUNT:ConfigArea.ADDR_ACCESS_COUNT+2] = current.to_bytes(2, 'big')
        return current
    
    def get_access_count(self):
        """获取接入计数"""
        return int.from_bytes(self.data[ConfigArea.ADDR_ACCESS_COUNT:ConfigArea.ADDR_ACCESS_COUNT+2], 'big')
    
    def set_encryption_mode(self, mode):
        """设置加密方式
        mode: 0-不加密, 1-AES128, 2-XOR, 3-凯撒加密, 4-RC4
        """
        self.data[ConfigArea.ADDR_ENCRYPTION] = mode & 0xFF
    
    def get_encryption_mode(self):
        """获取加密方式"""
        return self.data[ConfigArea.ADDR_ENCRYPTION]
    
    def format(self):
        """格式化配置区"""
        self.data = bytearray([0x00] * self.SIZE)