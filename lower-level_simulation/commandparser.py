#!/usr/bin/env python3
# 命令解析器模块

import hashlib

class CommandParser:
    """命令解析器"""
    
    def __init__(self, config_area, block_manager, key_value_manager):
        self.config_area = config_area
        self.block_manager = block_manager
        self.key_value_manager = key_value_manager
        self.is_authenticated = False  # 是否已通过密码验证
    
    def _parse_args(self, cmd):
        """解析命令参数，支持双引号包裹的字符串（处理空格问题）"""
        args = []
        current = ''
        in_quotes = False
        
        # 跳过 AT+
        i = 3
        while i < len(cmd):
            char = cmd[i]
            
            if char == '"':
                in_quotes = not in_quotes
            elif char == '+' and not in_quotes:
                # 结束当前参数，去掉两端的双引号
                args.append(current.strip('"'))
                current = ''
            else:
                current += char
            i += 1
        
        # 添加最后一个参数，去掉两端的双引号
        if current:
            args.append(current.strip('"'))
        
        return args
    
    def parse(self, cmd):
        """解析命令并执行"""
        cmd = cmd.strip()
        if not cmd.startswith('AT+'):
            return 'ERR+5'  # 未知错误
        
        parts = self._parse_args(cmd)
        if len(parts) < 1:
            return 'ERR+4'  # 参数错误
        
        cmd_type = parts[0]
        
        # 未初始化时只允许INIT命令
        if not self.config_area.is_initialized():
            if cmd_type == 'INIT':
                return self._handle_init(parts)
            else:
                return 'ERR+3'  # 未验证（未初始化）
        
        # 密码验证启用时需要先验证
        # 但如果没有设置密码（密码哈希为空），则直接通过验证
        if self.config_area.is_pwd_auth_enabled() and not self.is_authenticated:
            # 检查是否有密码设置
            pwd_hash = self.config_area.get_password_hash()
            if pwd_hash != b'\x00' * 32:  # 有密码设置
                if cmd_type != 'AUTH' or (len(parts) >= 3 and parts[2] != 'VERIFY'):
                    return 'ERR+3'  # 未验证
            else:
                # 没有设置密码，自动通过验证
                self.is_authenticated = True
        
        # 路由到对应处理函数
        handlers = {
            'INIT': self._handle_init,
            'ECHO': self._handle_echo,
            'INFO': self._handle_info,
            'STATUS': self._handle_status,
            'AUTH': self._handle_auth,
            'READ': self._handle_read,
            'WRITE': self._handle_write,
            'CREATE': self._handle_create,
            'DELETE': self._handle_delete,
            'UPDATE': self._handle_update,
            'GET': self._handle_get,
            'FORMAT': self._handle_format
        }
        
        handler = handlers.get(cmd_type)
        if handler:
            return handler(parts)
        else:
            return 'ERR+5'  # 未知错误
    
    def _handle_init(self, parts):
        """处理INIT命令"""
        if len(parts) < 3:
            return 'ERR+4'  # 参数错误
        
        device_name = parts[1]
        password = parts[2]
        
        # 格式化EEPROM
        self.key_value_manager.eeprom_manager.format()
        self.config_area.format()
        
        # 设置设备名称
        self.config_area.set_device_name(device_name)
        
        # 设置密码（UPASS表示不启用密码）
        if password != 'UPASS':
            pwd_hash = hashlib.sha256(password.encode()).digest()
            self.config_area.set_password_hash(pwd_hash)
            self.config_area.set_pwd_auth_enabled(True)
        else:
            self.config_area.set_pwd_auth_enabled(False)
        
        # 设置初始化标志
        self.config_area.set_initialized()
        
        return 'DATA+OK'
    
    def _handle_echo(self, parts):
        """处理ECHO命令"""
        return 'DATA+0'  # 成功
    
    def _handle_info(self, parts):
        """处理INFO命令"""
        name = self.config_area.get_device_name()
        pwd_status = 'ENABLED' if self.config_area.is_pwd_auth_enabled() else 'DISABLED'
        access_count = self.config_area.get_access_count()
        
        # 计算块数量和键值对数量
        blocks = self.block_manager.get_all_blocks()
        block_count = len(blocks)
        key_count = sum(block['key_addrs'].count(addr) for block in blocks for addr in block['key_addrs'] if addr != 0xFFFF)
        
        return f'INFO+{name}+{pwd_status}+{access_count}+{block_count}+{key_count}'
    
    def _handle_status(self, parts):
        """处理STATUS命令"""
        return 'STATUS+0'  # 0表示空闲
    
    def _handle_auth(self, parts):
        """处理AUTH命令"""
        if len(parts) < 3:
            return 'ERR+4'
        
        sub_cmd = parts[2]
        
        if sub_cmd == 'CREATE':
            if len(parts) < 4:
                return 'ERR+4'
            password = parts[3]
            pwd_hash = hashlib.sha256(password.encode()).digest()
            self.config_area.set_password_hash(pwd_hash)
            self.config_area.set_pwd_auth_enabled(True)
            return 'AUTH+0'
        
        elif sub_cmd == 'VERIFY':
            if len(parts) < 4:
                return 'ERR+4'
            password = parts[3]
            stored_hash = self.config_area.get_password_hash()
            
            if stored_hash == b'\x00' * 32:
                return 'AUTH+2'  # 未设置密码
            
            computed_hash = hashlib.sha256(password.encode()).digest()
            if computed_hash == stored_hash:
                self.is_authenticated = True
                return 'AUTH+0'  # 验证成功
            else:
                return 'AUTH+1'  # 验证失败
        
        elif sub_cmd == 'ENABLE':
            self.config_area.set_pwd_auth_enabled(True)
            return 'AUTH+0'
        
        elif sub_cmd == 'DISABLE':
            self.config_area.set_pwd_auth_enabled(False)
            return 'AUTH+0'
        
        elif sub_cmd == 'VERIFYOUT':
            self.is_authenticated = False
            return 'AUTH+0'
        
        return 'ERR+4'  # 参数错误
    
    def _handle_read(self, parts):
        """处理READ命令"""
        if len(parts) < 2:
            return 'ERR+4'
        
        read_type = parts[1]
        
        if read_type == 'BLOCK':
            if len(parts) < 3:
                return 'ERR+4'
            block_id = int(parts[2])
            block = self.block_manager.get_block_by_id(block_id)
            if not block:
                return '\EOF'
            
            # 返回格式: [块ID;块名|键地址1|键地址2|...]
            # 收集所有有效键地址（从BLOCK_ARR中读取）
            key_addrs = []
            keys = []
            block_arr = block['block_arr']
            for i in range(self.block_manager.max_keys_per_block):
                # 从BLOCK_ARR中读取5字节地址
                offset = i * 5
                if offset + 5 > len(block_arr):
                    continue
                chip = block_arr[offset]
                addr_11bit = int.from_bytes(bytes(block_arr[offset + 1:offset + 3]), 'big')
                # 检查是否为未使用状态（0xFF）
                if chip == 0xFF and addr_11bit == 0x7FF:
                    continue
                # 组合成12位地址
                addr = (chip << 11) | addr_11bit
                key_addrs.append(str(addr))
                # 读取键名
                data = self.key_value_manager.eeprom_manager.read(addr, 256)
                start = 1  # 跳过BLOCK_SLAVE
                sep_pos = data.find(bytes([0x1F]), start)
                if sep_pos > 0:
                    key = bytes(data[start:sep_pos]).decode('utf-8', errors='replace')
                    keys.append(key)
            
            # 组合结果: 块ID;块名|键地址群|键名群
            return f'DATA+{block_id};{block["name"]}|{"|".join(key_addrs)}|{"|".join(keys)}'
        
        elif read_type == 'KEY':
            if len(parts) < 4:
                return 'ERR+4'
            block_id = int(parts[2])
            key = parts[3]
            value = self.key_value_manager.read_key(block_id, key)
            if value is None:
                return '\\EOF'
            return f'DATA+{value}'
        
        else:
            return 'ERR+4'
    
    def _handle_write(self, parts):
        """处理WRITE命令"""
        if len(parts) < 4:
            return 'ERR+4'
        
        block_id = int(parts[1])
        key = parts[2]
        value = parts[3]
        
        # 检查块是否存在
        block = self.block_manager.get_block_by_id(block_id)
        if not block:
            return '\\EOF'
        
        # 检查是否已存在该键，存在则更新
        existing_value = self.key_value_manager.read_key(block_id, key)
        if existing_value is not None:
            success = self.key_value_manager.update_key(block_id, key, value)
        else:
            # 创建新键
            success = self.key_value_manager.create_key('1', str(block_id), key, value)
        
        return 'RESULT+0' if success else '\\EOF'
    
    def _handle_create(self, parts):
        """处理CREATE命令"""
        if len(parts) < 2:
            return 'ERR+4'
        
        create_type = parts[1]
        
        if create_type == 'BLOCK':
            if len(parts) < 3:
                return 'ERR+4'
            block_name = parts[2]
            block_size = int(parts[3]) if len(parts) > 3 else 16
            
            block_id = self.block_manager.create_block(block_name, block_size)
            if block_id < 0:
                return '\EOF'
            return f'DATA+{block_id}'
        
        elif create_type == 'KEY':
            if len(parts) < 6:
                return 'ERR+4'
            block_flag = parts[2]
            block_identifier = parts[3]
            key = parts[4]
            value = parts[5]
            
            success = self.key_value_manager.create_key(block_flag, block_identifier, key, value)
            return 'RESULT+0' if success else '\\EOF'
        
        return 'ERR+4'
    
    def _handle_delete(self, parts):
        """处理DELETE命令"""
        if len(parts) < 2:
            return 'ERR+4'
        
        delete_type = parts[1]
        
        if delete_type == 'BLOCK':
            if len(parts) < 3:
                return 'ERR+4'
            block_id = int(parts[2])
            
            success = self.block_manager.delete_block(block_id)
            return 'RESULT+0' if success else '\EOF'
        
        elif delete_type == 'KEY':
            if len(parts) < 5:
                return 'ERR+4'
            block_flag = parts[2]
            block_identifier = parts[3]
            key = parts[4]
            value = parts[5] if len(parts) > 5 else ''
            
            success = self.key_value_manager.delete_key(block_flag, block_identifier, key, value)
            return 'RESULT+0' if success else '\\EOF'
        
        return 'ERR+4'
    
    def _handle_update(self, parts):
        """处理UPDATE命令"""
        if len(parts) < 2:
            return 'ERR+4'
        
        update_type = parts[1]
        
        if update_type == 'BLOCK':
            if len(parts) < 3:
                return 'ERR+4'
            block_id = int(parts[2])
            block = self.block_manager.get_block_by_id(block_id)
            if not block:
                return '\\EOF'
            return 'RESULT+0'  # 预留功能
        
        elif update_type == 'KEY':
            if len(parts) < 5:
                return 'ERR+4'
            block_id = int(parts[2])
            key = parts[3]
            value = parts[4]
            
            success = self.key_value_manager.update_key(block_id, key, value)
            return 'RESULT+0' if success else '\\EOF'
        
        return 'ERR+4'
    
    def _handle_get(self, parts):
        """处理GET命令"""
        if len(parts) < 3:
            return 'ERR+4'
        
        if parts[1] == 'ALL' and parts[2] == 'BLOCK':
            blocks = self.block_manager.get_all_blocks()
            result = []
            
            for block in blocks:
                # 读取块内所有键值对（从BLOCK_ARR中读取）
                key_values = []
                block_arr = block['block_arr']
                for i in range(self.block_manager.max_keys_per_block):
                    # 从BLOCK_ARR中读取5字节地址
                    offset = i * 5
                    if offset + 5 > len(block_arr):
                        continue
                    chip = block_arr[offset]
                    addr_11bit = int.from_bytes(bytes(block_arr[offset + 1:offset + 3]), 'big')
                    # 检查是否为未使用状态（0xFF）
                    if chip == 0xFF and addr_11bit == 0x7FF:
                        continue
                    # 组合成12位地址
                    addr = (chip << 11) | addr_11bit
                    
                    data = self.key_value_manager.eeprom_manager.read(addr, 256)
                    start = 1  # 跳过BLOCK_SLAVE
                    sep_pos = data.find(bytes([0x1F]), start)
                    etx_pos = data.find(bytes([0x03]), sep_pos + 1)
                    if sep_pos > 0 and etx_pos > 0:
                        key = bytes(data[start:sep_pos]).decode('utf-8', errors='replace')
                        value = bytes(data[sep_pos + 1:etx_pos - 1]).decode('utf-8', errors='replace')
                        key_values.append(f'{key}={value}')
                
                # 格式: [块名;块id](键值对,键值对,...)
                kv_str = ','.join(key_values)
                result.append(f'[{block["name"]};{block["id"]}]({kv_str})')
            
            # 多个块之间用|分隔
            return 'DATA+' + '|'.join(result)
        
        return 'ERR+4'
    
    def _handle_format(self, parts):
        """处理FORMAT命令"""
        if len(parts) < 2:
            return 'ERR+4'
        
        format_type = parts[1]
        
        if format_type == 'DEV':
            self.key_value_manager.eeprom_manager.format()
            self.config_area.format()
            return 'RESULT+0'
        
        elif format_type == 'BLOCK':
            if len(parts) < 4:
                return 'ERR+4'
            block_flag = parts[2]
            block_identifier = parts[3]
            
            if block_flag == '0':
                block = self.block_manager.get_block_by_name(block_identifier)
            else:
                block = self.block_manager.get_block_by_id(int(block_identifier))
            
            if not block:
                return '\\EOF'
            
            # 删除块中所有键值对（24c64模拟块内键地址格式化为0x00）
            block_arr = block['block_arr']
            for i in range(self.block_manager.max_keys_per_block):
                # 从BLOCK_ARR中读取5字节地址
                offset = i * 5
                if offset + 5 > len(block_arr):
                    continue
                chip = block_arr[offset]
                addr_11bit = int.from_bytes(bytes(block_arr[offset + 1:offset + 3]), 'big')
                # 检查是否为未使用状态（0xFF）
                if chip == 0xFF and addr_11bit == 0x7FF:
                    continue
                # 组合成12位地址
                addr = (chip << 11) | addr_11bit
                
                # 格式化24c64中的键值对为0x00
                data = self.key_value_manager.eeprom_manager.read(addr, 256)
                etx_pos = data.find(b'\x03')
                if etx_pos > 0:
                    blank_data = b'\x00' * (etx_pos + 1)
                else:
                    blank_data = b'\x00' * 32
                self.key_value_manager.eeprom_manager.write(addr, blank_data)
            
            # 重置块记录中的BLOCK_ARR为0x00（24c64模拟块内键地址格式化为0x00）
            record_addr = self.block_manager.BLOCK_RECORD_START_ADDR + (block['id'] - 1) * self.block_manager.BLOCK_RECORD_SIZE
            record = bytearray(self.config_area.data[record_addr:record_addr+self.block_manager.BLOCK_RECORD_SIZE])
            for i in range(self.block_manager.max_keys_per_block):
                arr_offset = self.block_manager.OFFSET_BLOCK_ARR + i * 5
                record[arr_offset:arr_offset + 5] = b'\x00' * 5
            self.config_area.data[record_addr:record_addr+self.block_manager.BLOCK_RECORD_SIZE] = record
            
            return 'RESULT+0'
        
        return 'ERR+4'