#!/usr/bin/env python3
# 键值对管理模块

class KeyValueManager:
    """键值对管理器"""
    
    # 键值对结构定义
    BLOCK_SLAVE_SIZE = 1
    KEY_VALUE_SEPARATOR = 0x1F
    VER_SIZE = 1
    ETX = 0x03
    
    def __init__(self, config_area, eeprom_manager, block_manager):
        self.config_area = config_area
        self.eeprom_manager = eeprom_manager
        self.block_manager = block_manager
        self.next_free_addr = 0x800  # 键值对存储起始地址
    
    def find_free_addr(self):
        """查找空闲地址"""
        # 简单实现：从起始地址向后查找
        # 12位地址最大为 0x1FFF（两片24C64，每片8KB）
        max_addr = 0x1FFF
        for addr in range(self.next_free_addr, max_addr + 1):
            data = self.eeprom_manager.read(addr, 1)
            if data[0] == 0x00 or data[0] == 0xFF:
                self.next_free_addr = addr + 1
                return addr
        return -1
    
    def create_key(self, block_flag, block_identifier, key, value):
        """创建键值对"""
        # 获取块信息
        if block_flag == '0':
            block = self.block_manager.get_block_by_name(block_identifier)
        else:
            block = self.block_manager.get_block_by_id(int(block_identifier))
        
        if not block:
            print(f"DEBUG: create_key - block not found for flag={block_flag}, identifier={block_identifier}")
            return False
        
        # 检查块中是否已有该键（使用BLOCK_ARR）
        if 'block_arr' not in block:
            print(f"DEBUG: create_key - block_arr not in block")
            return False
        block_arr = block['block_arr']
        if block_arr is None or len(block_arr) == 0:
            print(f"DEBUG: create_key - block_arr is None or empty")
            return False
        for i in range(self.block_manager.max_keys_per_block):
            addr = self._get_addr_from_block_arr(block_arr, i)
            if addr != 0xFFFF:
                existing_key = self._read_key(addr)
                if existing_key == key:
                    return False  # 键已存在
        
        # 查找空闲键位置（使用BLOCK_ARR）
        for i in range(self.block_manager.max_keys_per_block):
            addr = self._get_addr_from_block_arr(block_arr, i)
            if addr == 0xFFFF:
                # 查找空闲存储地址
                storage_addr = self.find_free_addr()
                if storage_addr < 0:
                    return False
                
                # 写入键值对
                kv_data = self._build_key_value(block['id'], key, value)
                self.eeprom_manager.write(storage_addr, kv_data)
                
                # 更新块记录中的键地址（使用BLOCK_ARR，5字节/地址）
                record_addr = self.block_manager.BLOCK_RECORD_START_ADDR + (block['id'] - 1) * self.block_manager.BLOCK_RECORD_SIZE
                # 确保记录地址不越界
                if record_addr + self.block_manager.BLOCK_RECORD_SIZE > len(self.config_area.data):
                    return False
                record = bytearray(self.config_area.data[record_addr:record_addr+self.block_manager.BLOCK_RECORD_SIZE])
                # 将12位地址写入5字节（第1字节芯片选择，第2-3字节地址）
                chip = (storage_addr >> 11) & 0xFF
                addr_11bit = storage_addr & 0x7FF
                arr_offset = self.block_manager.OFFSET_BLOCK_ARR + i * 5
                # 确保偏移量不越界
                if arr_offset + 5 > len(record):
                    return False
                record[arr_offset] = chip
                record[arr_offset + 1:arr_offset + 3] = addr_11bit.to_bytes(2, 'big')
                record[arr_offset + 3:arr_offset + 5] = b'\x00\x00'  # 预留2字节
                self.config_area.data[record_addr:record_addr+self.block_manager.BLOCK_RECORD_SIZE] = record
                return True
        
        return False  # 块已满
    
    def _get_addr_from_block_arr(self, block_arr, index):
        """从BLOCK_ARR中读取键值对地址"""
        # 添加详细的边界检查
        if block_arr is None:
            return 0xFFFF
        
        offset = index * 5
        # 确保偏移量不越界
        if offset + 3 > len(block_arr):
            return 0xFFFF
        
        chip = block_arr[offset]
        # 检查是否为未使用状态（芯片选择为0xFF表示未使用）
        if chip == 0xFF:
            return 0xFFFF
        
        # 读取11位地址
        try:
            addr_11bit = int.from_bytes(bytes(block_arr[offset + 1:offset + 3]), 'big')
        except:
            return 0xFFFF
        
        # 组合成12位地址
        return (chip << 11) | addr_11bit
    
    def _build_key_value(self, block_id, key, value):
        """构建键值对数据"""
        key_bytes = key.encode('utf-8')
        value_bytes = value.encode('utf-8')
        data = bytearray()
        data.append(block_id & 0xFF)  # BLOCK_SLAVE
        data.extend(key_bytes)
        data.append(self.KEY_VALUE_SEPARATOR)  # 分隔符
        data.extend(value_bytes)
        data.append(0x01)  # VER
        data.append(self.ETX)  # ETX
        return data
    
    def _read_key(self, addr):
        """读取键"""
        data = self.eeprom_manager.read(addr, 256)
        start = self.BLOCK_SLAVE_SIZE
        sep_pos = data.find(bytes([self.KEY_VALUE_SEPARATOR]), start)
        if sep_pos > 0:
            return bytes(data[start:sep_pos]).decode('utf-8', errors='replace')
        return ""
    
    def read_key(self, block_id, key):
        """读取键值"""
        block = self.block_manager.get_block_by_id(block_id)
        if not block:
            return None
        
        for i in range(self.block_manager.max_keys_per_block):
            addr = self._get_addr_from_block_arr(block['block_arr'], i)
            if addr != 0xFFFF:
                data = self.eeprom_manager.read(addr, 256)
                start = self.BLOCK_SLAVE_SIZE
                sep_pos = data.find(bytes([self.KEY_VALUE_SEPARATOR]), start)
                if sep_pos > 0:
                    current_key = bytes(data[start:sep_pos]).decode('utf-8', errors='replace')
                    if current_key == key:
                        etx_pos = data.find(bytes([self.ETX]), sep_pos + 1)
                        if etx_pos > 0:
                            return bytes(data[sep_pos + 1:etx_pos - 1]).decode('utf-8', errors='replace')
        return None
    
    def delete_key(self, block_flag, block_identifier, key, value):
        """删除键值对"""
        if block_flag == '0':
            block = self.block_manager.get_block_by_name(block_identifier)
        else:
            block = self.block_manager.get_block_by_id(int(block_identifier))
        
        if not block:
            return False
        
        for i in range(self.block_manager.max_keys_per_block):
            addr = self._get_addr_from_block_arr(block['block_arr'], i)
            if addr != 0xFFFF:
                data = self.eeprom_manager.read(addr, 256)
                start = self.BLOCK_SLAVE_SIZE
                sep_pos = data.find(bytes([self.KEY_VALUE_SEPARATOR]), start)
                if sep_pos > 0:
                    current_key = bytes(data[start:sep_pos]).decode('utf-8', errors='replace')
                    if current_key == key:
                        # 删除键值对
                        self.block_manager._delete_key_value(addr)
                        
                        # 更新块记录（使用BLOCK_ARR，5字节/地址）
                        record_addr = self.block_manager.BLOCK_RECORD_START_ADDR + (block['id'] - 1) * self.block_manager.BLOCK_RECORD_SIZE
                        record = bytearray(self.config_area.data[record_addr:record_addr+self.block_manager.BLOCK_RECORD_SIZE])
                        arr_offset = self.block_manager.OFFSET_BLOCK_ARR + i * 5
                        record[arr_offset:arr_offset + 5] = b'\xFF' * 5  # 重置为未使用状态
                        self.config_area.data[record_addr:record_addr+self.block_manager.BLOCK_RECORD_SIZE] = record
                        return True
        return False
    
    def update_key(self, block_id, key, new_value):
        """更新键值"""
        block = self.block_manager.get_block_by_id(block_id)
        if not block:
            return False
        
        for i in range(self.block_manager.max_keys_per_block):
            addr = self._get_addr_from_block_arr(block['block_arr'], i)
            if addr != 0xFFFF:
                data = bytearray(self.eeprom_manager.read(addr, 256))
                start = self.BLOCK_SLAVE_SIZE
                sep_pos = data.find(bytes([self.KEY_VALUE_SEPARATOR]), start)
                if sep_pos > 0:
                    current_key = bytes(data[start:sep_pos]).decode('utf-8', errors='replace')
                    if current_key == key:
                        etx_pos = data.find(bytes([self.ETX]), sep_pos + 1)
                        if etx_pos > 0:
                            # 清除旧值
                            new_data = data[:sep_pos + 1]
                            new_data.extend(new_value.encode('utf-8'))
                            new_data.append(0x01)  # VER
                            new_data.append(self.ETX)  # ETX
                            self.eeprom_manager.write(addr, new_data)
                            return True
        return False