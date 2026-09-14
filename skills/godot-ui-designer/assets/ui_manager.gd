# ui_manager.gd — Godot 4 专业 UI 基类/管理器模板
#
# 用法：
#   1. 把本文件作为 UI 子控件的基类——让具体 UI 脚本 `extends UIBase`。
#   2. 重写 _bind_signals() 订阅游戏状态（信号/事件总线），在 _update_* 中只改显示，绝不轮询。
#   3. 需要平滑变化时调用 tween_label_number() / tween_bar_value()。
#   4. 把 UIManager 注册为 Autoload（名称 UIManager），用于跨场景事件中转与界面栈管理。
#
# 设计原则：UI 只响应事件，不主动每帧拉取游戏状态。

extends Control
class_name UIBase

# ---------- 通用工具 ----------

## 安全获取子节点，缺失时仅在编辑器告警，避免运行时崩溃。
func safe_get(path: NodePath) -> Node:
	if has_node(path):
		return get_node(path)
	push_warning("UIBase: 节点路径缺失 %s @ %s" % [path, name])
	return null

## 数字滚动过渡（血条数值、分数等）。
func tween_label_number(target: Label, from: float, to: float, duration := 0.25) -> void:
	var t := create_tween()
	t.tween_method(func(v): target.text = str(roundi(v)), from, to, duration)

## 进度/血条平滑过渡，配合 Label 同步。
func tween_bar_value(bar: Range, to: float, label: Label = null, duration := 0.25) -> void:
	var t := create_tween()
	t.tween_property(bar, "value", to, duration)
	if label != null:
		var from := bar.value
		t.parallel().tween_method(func(v): label.text = str(roundi(v)), from, to, duration)

# ---------- 生命周期（子类重写钩子） ----------

func _ready() -> void:
	_bind_signals()

## 子类在此连接信号/事件总线。
func _bind_signals() -> void:
	pass

# ============================================================
# UIManager —— Autoload 单例（在 Project Settings > Autoload 注册名为 UIManager）
# 负责：界面栈（push/pop）、全局 UI 事件中转、显隐总开关。
# ============================================================
class_name UIManager
extends Node

var _stack: Array[Control] = []

## 压入一个界面并显示（常用于菜单/弹窗层级）。
func push(ui: Control) -> void:
	if not ui.visible:
		ui.visible = true
	_stack.push_back(ui)

## 弹出栈顶界面。
func pop() -> void:
	if _stack.is_empty():
		return
	var top := _stack.pop_back()
	if is_instance_valid(top):
		top.visible = false

## 全局广播：游戏逻辑发事件，各 UI 订阅。替代跨场景硬引用。
signal game_event(event_name: String, payload: Variant)
func emit(event_name: String, payload: Variant = null) -> void:
	game_event.emit(event_name, payload)

# ---------- 示例：具体 UI 如何使用 ----------

# extends UIBase
# class_name HealthHUD
# @onready var bar: ProgressBar = safe_get(^"VBox/HPBar")
# @onready var label: Label = safe_get(^"VBox/HPText")
# func _bind_signals() -> void:
#     # 订阅角色生命变化（信号驱动，不轮询）
#     GameState.player_health_changed.connect(_on_hp_changed)
# func _on_hp_changed(cur: float, maxv: float) -> void:
#     bar.max_value = maxv
#     tween_bar_value(bar, cur, label)
#
# # 通过事件总线跨场景通信：
# func _on_some_event(_n: String, _p: Variant) -> void:
#     UIManager.game_event.connect(_on_some_event)
