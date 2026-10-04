# -*- coding: utf-8 -*-
import subprocess, io

# 1) 取 git HEAD 的中文版作为基底（结构完好、纯中文）
raw = subprocess.run(['git', 'cat-file', 'blob', 'HEAD:./I18n/Strings-zh_CN.resx'],
                     capture_output=True).stdout
base = raw.decode('utf-8-sig')

# 2) 需要补充的中文键值（对应英文版新增的键）
extra = [
    ('_block_size', u'块大小'),
    ('_connect', u'连接'),
    ('_device_connected', u'设备已连接'),
    ('_device_disconnect', u'断开设备'),
    ('_device_disconnected', u'设备已断开'),
    ('_not_archivumu_device', u'不是 ArchivumU 设备或串口被占用'),
    ('_port_busy', u'串口被占用'),
    ('_auth_required', u'该设备需要认证'),
    ('_auth_success', u'认证成功'),
    ('_block_add_success', u'块已创建'),
    ('_block_add_failed', u'创建块失败'),
    ('_block_del_success', u'块已删除'),
    ('_block_del_failed', u'删除块失败'),
    ('_key_add_success', u'键已创建'),
    ('_key_add_failed', u'创建键失败'),
    ('_key_del_success', u'键已删除'),
    ('_key_del_failed', u'删除键失败'),
    ('_please_select_block', u'请先选择一个块'),
    ('_confirm_del_block', u'确认删除选中的块？'),
]

block = u''
for k, v in extra:
    block += u'    <data name="%s" xml:space="preserve">\n        <value>%s</value>\n    </data>\n' % (k, v)

# 3) 在 </root> 前插入
marker = u'</root>'
assert marker in base
out = base.replace(marker, block + marker, 1)

# 4) 以带 BOM 的 utf-8 写回
with io.open('I18n/Strings-zh_CN.resx', 'w', encoding='utf-8-sig', newline='') as f:
    f.write(out)

print('OK, len =', len(out))
