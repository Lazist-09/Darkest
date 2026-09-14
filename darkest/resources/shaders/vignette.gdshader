// 🔴 `ui_spec §12.3`：**暗角 / 闪白 用【材质统一实现】**（取代逐节点样式）。
//    🔴 视觉规范（强度 / 颜色 / 时长）仍属 `ui_spec §1.4` + 策划 `#321`⑤；**材质只是它的一致实现** ✓
// ⚠️ **文字描边不在这里** —— 那是 `Label`/`Button` 的【引擎内置】主题项（`font_outline_color` + `outline_size`），
//    按红线 26「引擎内置已能做不得自研」用内置，见 `DdTheme` 的描边段 ✓
shader_type canvas_item;

// 暗角强度（`#321`⑤：士气崩溃 = 0.40）
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.0;
uniform vec4 vignette_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);

// 闪白强度（0 = 关闭；阵亡/重击的短促高对比）
uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
	// 径向暗角：中心透明 → 四角压暗（用 1.4142 把"半对角"归一化到 1）
	vec2 d = UV - vec2(0.5);
	float r = length(d) * 1.41421356;
	float v = smoothstep(0.35, 1.0, r) * vignette_strength;
	vec4 col = vec4(vignette_color.rgb, v);

	// 闪白：叠加（不是替换）⇒ 底下的 UI 仍看得见
	float f = flash;
	COLOR = mix(col, vec4(flash_color.rgb, max(col.a, f)), f);
}
