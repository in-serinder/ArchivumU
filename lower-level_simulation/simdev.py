#!/usr/bin/env python3
# 主模拟程序 - 通过COM1串口进行通信

import serial
import time
import threading
import os
import sys

# 添加当前目录到路径
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from eeprom import EEPROMManager
from configarea import ConfigArea
from blockmanager import BlockManager
from keyvaluemanager import KeyValueManager
from commandparser import CommandParser

class SimDevice:
    """模拟设备主类"""
    
    def __init__(self, port='COM1', baud_rate=9600):
        self.port = port
        self.baud_rate = baud_rate
        self.ser = None
        self.running = False
        self.echo_timer = None
        self.echo_interval = 5  # 5秒回响周期
        
        # 初始化存储组件
        self.eeprom_manager = EEPROMManager()
        self.config_area = ConfigArea()
        self.block_manager = BlockManager(self.config_area, self.eeprom_manager)
        self.key_value_manager = KeyValueManager(self.config_area, self.eeprom_manager, self.block_manager)
        self.command_parser = CommandParser(self.config_area, self.block_manager, self.key_value_manager)
    
    def connect(self):
        """连接到串口"""
        try:
            self.ser = serial.Serial(
                port=self.port,
                baudrate=self.baud_rate,
                parity=serial.PARITY_NONE,
                stopbits=serial.STOPBITS_ONE,
                bytesize=serial.EIGHTBITS,
                timeout=1
            )
            print(f"已连接到 {self.port}")
            return True
        except Exception as e:
            print(f"连接失败: {e}")
            return False
    
    def start(self):
        """启动模拟设备"""
        if not self.connect():
            return
        
        self.running = True
        self._reset_echo_timer()
        
        # 启动读取线程
        read_thread = threading.Thread(target=self._read_loop)
        read_thread.daemon = True
        read_thread.start()
        
        print("设备模拟已启动")
        
        try:
            while self.running:
                time.sleep(1)
        except KeyboardInterrupt:
            self.stop()
    
    def stop(self):
        """停止模拟设备"""
        self.running = False
        if self.echo_timer:
            self.echo_timer.cancel()
        # 保存数据到文件
        self.eeprom_manager.save_all()
        self.config_area.save_to_file()
        if self.ser:
            self.ser.close()
        print("设备模拟已停止")
    
    def _reset_echo_timer(self):
        """重置回响定时器"""
        if self.echo_timer:
            self.echo_timer.cancel()
        self.echo_timer = threading.Timer(self.echo_interval, self._send_echo)
        self.echo_timer.daemon = True
        self.echo_timer.start()
    
    def _send_echo(self):
        """发送回响信号"""
        if self.running and self.config_area.is_initialized():
            self._send_response('ECHO')
    
    def _read_loop(self):
        """读取串口数据循环"""
        while self.running:
            try:
                if self.ser and self.ser.in_waiting > 0:
                    data = self.ser.readline().decode('utf-8').strip()
                    if data:
                        print(f"收到指令: {data}")
                        self._reset_echo_timer()  # 有交互，重置回响定时器
                        response = self.command_parser.parse(data)
                        self._send_response(response)
            except Exception as e:
                print(f"读取错误: {e}")
                break
    
    def _send_response(self, response):
        """发送响应到串口"""
        try:
            if self.ser:
                self.ser.write((response + '\r\n').encode('utf-8'))
                print(f"发送响应: {response}")
                # 每次响应后立即保存数据到文件
                self.eeprom_manager.save_all()
                self.config_area.save_to_file()
                print("数据已保存到文件")
        except Exception as e:
            print(f"发送错误: {e}")

if __name__ == '__main__':
    device = SimDevice(port='COM1', baud_rate=9600)
    device.start()