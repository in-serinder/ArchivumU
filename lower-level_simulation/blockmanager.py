#!/usr/bin/env python3
# 块结构和键值对管理模块

import struct

class BlockManager:
    """块管理器"""
    
    # 块结构大小定义
    BLOCK_NAME_SIZE = 20
    BLOCK_ID_SIZE = 2
    BLOCK_SIZE_SIZE = 2
    KEY_ADDR_SIZE = 32  # 16个键值对，每个2字节地址
    BLOCK_ARR_SIZE = 80  # 5字节 * 16个键值对
    BLOCK_VER_SIZE = 1
    
    # 块结构偏移
    OFFSET_NAME = 0
    OFFSET_ID = OFFSET_NAME + BLOCK_NAME_SIZE  # 20
    OFFSET_SIZE = OFFSET_ID + BLOCK_ID_SIZE  # 22
    OFFSET_KEY_ADDR = OFFSET_SIZE + BLOCK_SIZE_SIZE  # 24
    OFFSET_BLOCK_ARR = OFFSET_KEY_ADDR + KEY_ADDR_SIZE  # 56
    OFFSET_VER = OFFSET_BLOCK_ARR + BLOCK_ARR_SIZE  # 136
    
    BLOCK_RECORD_SIZE = OFFSET_VER + BLOCK_VER_SIZE  # 137字节
    
    # 块记录起始地址（从片内配置区0x4F开始）
    BLOCK_RECORD_START_ADDR = 0x4F
    
    def __init__(self, config_area, eeprom_manager):
        self.config_area = config_area
        self.eeprom_manager = eeprom_manager
        self.max_blocks = 16
        self.max_keys_per_block = 16
        
    def create_block(self, block_name, block_size=16):
        """创建新块"""
        # 查找空闲块位置
        block_idx = self._find_free_block_index()
        if block_idx < 0:
            return -1  # 块数量已满
        
        # 计算块记录地址（从0x4F开始，每个块记录57字节）
        record_addr = self.BLOCK_RECORD_START_ADDR + block_idx * self.BLOCK_RECORD_SIZE
        
        # 设置块名
        name_bytes = block_name.encode('utf-8')[:self.BLOCK_NAME_SIZE]
        name_bytes += b'\x00' * (self.BLOCK_NAME_SIZE - len(name_bytes))
        
        # 设置块ID
        block_id = block_idx + 1  # 块ID从1开始
        
        # 设置块大小
        block_size = min(block_size, self.max_keys_per_block)
        
        # 构建块记录
        record = bytearray(self.BLOCK_RECORD_SIZE)
        record[self.OFFSET_NAME:self.OFFSET_NAME+self.BLOCK_NAME_SIZE] = name_bytes
        record[self.OFFSET_ID:self.OFFSET_ID+self.BLOCK_ID_SIZE] = block_id.to_bytes(2, 'big')
        record[self.OFFSET_SIZE:self.OFFSET_SIZE+self.BLOCK_SIZE_SIZE] = block_size.to_bytes(2, 'big')
        
        # 初始化KEY_ADDR为0xFFFF（未使用）
        for i in range(self.max_keys_per_block):
            record[self.OFFSET_KEY_ADDR + i * 2:self.OFFSET_KEY_ADDR + i * 2 + 2] = b'\xFF\xFF'
        
        # 初始化BLOCK_ARR为0xFF
        record[self.OFFSET_BLOCK_ARR:self.OFFSET_BLOCK_ARR+self.BLOCK_ARR_SIZE] = b'\xFF' * self.BLOCK_ARR_SIZE
        
        # 设置校验位
        record[self.OFFSET_VER] = 0x01
        
        # 写入片内配置区
        self.config_area.data[record_addr:record_addr+self.BLOCK_RECORD_SIZE] = record
        return block_id
    
    def _find_free_block_index(self):
        """查找空闲块索引"""
        for i in range(self.max_blocks):
            record_addr = self.BLOCK_RECORD_START_ADDR + i * self.BLOCK_RECORD_SIZE
            # 从片内配置区读取块记录
            record = bytes(self.config_area.data[record_addr:record_addr+self.BLOCK_RECORD_SIZE])
            if record[self.OFFSET_VER] != 0x01:
                return i
        return -1
    
    def get_block_by_id(self, block_id):
        """根据块ID获取块信息"""
        for i in range(self.max_blocks):
            record_addr = self.BLOCK_RECORD_START_ADDR + i * self.BLOCK_RECORD_SIZE
            # 从片内配置区读取块记录
            record = bytes(self.config_area.data[record_addr:record_addr+self.BLOCK_RECORD_SIZE])
            if record[self.OFFSET_VER] == 0x01:
                current_id = int.from_bytes(record[self.OFFSET_ID:self.OFFSET_ID+self.BLOCK_ID_SIZE], 'big')
                if current_id == block_id:
                    return self._parse_block_record(record)
        return None
    
    def get_block_by_name(self, block_name):
        """根据块名获取块信息"""
        for i in range(self.max_blocks):
            record_addr = self.BLOCK_RECORD_START_ADDR + i * self.BLOCK_RECORD_SIZE
            # 从片内配置区读取块记录
            record = bytes(self.config_area.data[record_addr:record_addr+self.BLOCK_RECORD_SIZE])
            if record[self.OFFSET_VER] == 0x01:
                name = bytes(record[self.OFFSET_NAME:self.OFFSET_NAME+self.BLOCK_NAME_SIZE]).decode('utf-8').rstrip('\x00')
                if name == block_name:
                    return self._parse_block_record(record)
        return None
    
    def _parse_block_record(self, record):
        """解析块记录"""
        # 确保记录长度足够
        record_len = len(record)
        
        # 解析名称
        name_end = min(self.OFFSET_NAME + self.BLOCK_NAME_SIZE, record_len)
        name_bytes = bytes(record[self.OFFSET_NAME:name_end])
        if len(name_bytes) < self.BLOCK_NAME_SIZE:
            name_bytes += b'\x00' * (self.BLOCK_NAME_SIZE - len(name_bytes))
        name = name_bytes.decode('utf-8').rstrip('\x00')
        
        # 解析ID
        if self.OFFSET_ID + self.BLOCK_ID_SIZE <= record_len:
            block_id = int.from_bytes(record[self.OFFSET_ID:self.OFFSET_ID+self.BLOCK_ID_SIZE], 'big')
        else:
            block_id = 0
        
        # 解析大小
        if self.OFFSET_SIZE + self.BLOCK_SIZE_SIZE <= record_len:
            block_size = int.from_bytes(record[self.OFFSET_SIZE:self.OFFSET_SIZE+self.BLOCK_SIZE_SIZE], 'big')
        else:
            block_size = 0
        
        # 解析键地址
        key_addrs = []
        for i in range(self.max_keys_per_block):
            addr_start = self.OFFSET_KEY_ADDR + i * 2
            addr_end = addr_start + 2
            if addr_end <= record_len:
                addr = int.from_bytes(record[addr_start:addr_end], 'big')
            else:
                addr = 0xFFFF
            key_addrs.append(addr)
        
        # 解析BLOCK_ARR（5字节 * 16 = 80字节）
        block_arr_end = min(self.OFFSET_BLOCK_ARR + self.BLOCK_ARR_SIZE, record_len)
        block_arr = list(record[self.OFFSET_BLOCK_ARR:block_arr_end])
        # 如果长度不足，填充0xFF
        if len(block_arr) < self.BLOCK_ARR_SIZE:
            block_arr.extend([0xFF] * (self.BLOCK_ARR_SIZE - len(block_arr)))
        
        # 解析版本
        if self.OFFSET_VER < record_len:
            ver = record[self.OFFSET_VER]
        else:
            ver = 0
        
        return {
            'name': name,
            'id': block_id,
            'size': block_size,
            'key_addrs': key_addrs,
            'block_arr': block_arr,
            'ver': ver
        }
    
    def delete_block(self, block_id):
        """删除块"""
        block_info = self.get_block_by_id(block_id)
        if not block_info:
            return False
        
        # 删除所有键值对
        for addr in block_info['key_addrs']:
            if addr != 0xFFFF:
                self._delete_key_value(addr)
        
        # 清除块记录
        block_idx = block_id - 1
        record_addr = self.BLOCK_RECORD_START_ADDR + block_idx * self.BLOCK_RECORD_SIZE
        blank_record = b'\xFF' * self.BLOCK_RECORD_SIZE
        self.config_area.data[record_addr:record_addr+self.BLOCK_RECORD_SIZE] = blank_record
        return True
    
    def _delete_key_value(self, addr):
        """删除键值对"""
        # 查找并清除键值对
        data = self.eeprom_manager.read(addr, 256)
        etx_pos = data.find(b'\x03')
        if etx_pos > 0:
            blank_data = b'\xFF' * (etx_pos + 1)
            self.eeprom_manager.write(addr, blank_data)
    
    def get_all_blocks(self):
        """获取所有块信息"""
        blocks = []
        for i in range(self.max_blocks):
            record_addr = self.BLOCK_RECORD_START_ADDR + i * self.BLOCK_RECORD_SIZE
            # 从片内配置区读取块记录
            record = bytes(self.config_area.data[record_addr:record_addr+self.BLOCK_RECORD_SIZE])
            if record[self.OFFSET_VER] == 0x01:
                blocks.append(self._parse_block_record(record))
        return blocks