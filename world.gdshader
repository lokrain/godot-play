shader_type spatial;

void fragment() {
    vec3 normal = normalize(NORMAL);
    ALBEDO = normal * 0.5 + 0.5; // Normalize the normal vector to a color value
}
