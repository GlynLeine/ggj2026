void DisableOnShadowPass_float(out float alpha)
{
#if defined(SHADERGRAPH_PREVIEW) || defined(SHADERGRAPH_PREVIEW_MAIN)
    alpha = 1.0;
#else
    alpha = (SHADERPASS == SHADERPASS_SHADOWCASTER) ? 0.0 : 1.0;
#endif
}

void DisableOnShadowPass_half(out half alpha)
{
    #if defined(SHADERGRAPH_PREVIEW) || defined(SHADERGRAPH_PREVIEW_MAIN)
    alpha = 1.0;
    #else
    alpha = (SHADERPASS == SHADERPASS_SHADOWCASTER) ? 0.0 : 1.0;
    #endif
}