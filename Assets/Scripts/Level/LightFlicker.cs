using UnityEngine;

// Candle and torch flicker: wobbles the light's intensity around its starting value with Perlin noise.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
	[Range(0f, 1f)] public float Amount = 0.2f;
	public float Speed = 6f;

	private Light _light;
	private float _baseIntensity;
	private float _seed;

	private void Awake()
	{
		_light = GetComponent<Light>();
		_baseIntensity = _light.intensity;
		_seed = Random.value * 100f;
	}

	private void Update()
	{
		float noise = Mathf.PerlinNoise(_seed, Time.time * Speed) * 2f - 1f;
		_light.intensity = _baseIntensity * (1f + noise * Amount);
	}
}
