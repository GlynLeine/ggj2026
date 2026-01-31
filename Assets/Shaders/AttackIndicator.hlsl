float circleAlpha(float2 texcoord, float2 scale, float fill, float coneRadius)
{
    const float FULL_LINE_WIDTH = 0.0625;
    const float FRONT_FILL_FADE_START = 0.33333333333;
    const float FRONT_FILL_FADE_END_OFFSET = 0.5;

    const float DEG2RAD = 0.017453296;
    const float RAD2DEG = 57.29577951;

    const float CIRCLE_LINE_DOT_WIDTH_DEGREES = 36.0;
    const float CIRCLE_CENTER_DEAD_AREA = 0.0125;
    const float CIRCLE_CENTER_FADE_POWER = 10.0;
    const float CIRCLE_LINE_DOT_HALF_WIDTH_DEGREES = CIRCLE_LINE_DOT_WIDTH_DEGREES * 0.5;
    const float CIRCLE_LINE_DOT_DOUBLE_WIDTH_DEGREES = CIRCLE_LINE_DOT_WIDTH_DEGREES * 2.0;

    float2 centeredCoords = texcoord * 2.0 - (1.0).xx;
    float radiusCoord = length(centeredCoords);
    float circleRadius = fill;

    float lineScale = 1.0 / scale.x;
    float scaledFullLineWidth = FULL_LINE_WIDTH * lineScale;
    float lineWidth = circleRadius * scaledFullLineWidth;

    float fillfade = saturate(smoothstep(0.0, circleRadius, radiusCoord) * 0.25);

    float fillEdgeLine = step(circleRadius - lineWidth, radiusCoord);
    float fillAreaExclude = step(radiusCoord, circleRadius);
    
    // Initial fill shape fading to the middle with the 100% fill line around it.
    float alpha = saturate(fillfade + fillEdgeLine) * fillAreaExclude;

    // Fade out the animation towards the center nicely.
    float centerFadeout = 1.0 - pow(saturate(1.0 - (radiusCoord + CIRCLE_CENTER_DEAD_AREA)), CIRCLE_CENTER_FADE_POWER);
    alpha *= centerFadeout;

    float cosAngle = dot(normalize(centeredCoords), float2(0.0, -1.0));
    float radius = 180.0 - coneRadius * 0.5;
    float angle = acos(cosAngle) * RAD2DEG;

    // Dotted outline
    float dotting = step(CIRCLE_LINE_DOT_WIDTH_DEGREES, fmod(angle * scale.x + CIRCLE_LINE_DOT_HALF_WIDTH_DEGREES, CIRCLE_LINE_DOT_DOUBLE_WIDTH_DEGREES));
    float outerLineExclude = step(1.0 - scaledFullLineWidth, radiusCoord) - step(1.0, radiusCoord);
    alpha += dotting * outerLineExclude;

    // Fade fill on the sides for cones
    float innerCircleExclude = step(radiusCoord, 1.0 - scaledFullLineWidth);
    float coneAngleFillFade = smoothstep(FRONT_FILL_FADE_START, 0.0 - FRONT_FILL_FADE_END_OFFSET, (angle - radius) / radius);
    alpha += fillfade * coneAngleFillFade * innerCircleExclude;

    alpha = saturate(alpha);

    // Cutout area for cones
    float coneExclude = step(cosAngle, cos(radius * DEG2RAD));
    alpha *= coneExclude;

    return alpha;
}

float rectAlpha(float2 texcoord, float2 scale, float fill, float useArrow)
{
    const float FULL_LINE_WIDTH = 0.0625;
    const float FRONT_FILL_FADE_START = 0.33333333333;
    const float FRONT_FILL_FADE_END_OFFSET = 0.5;

    const float FILL_FADE_SIZE = 1.25;
    const float FILL_FADE_OFFSET = 0.125;
    const float2 ARROW_ORIGIN_OFFSET = float2(0.0, 0.75);
    const float COS_ARROW_HEAD_POINT_ANGLE = 0.57357643635104609610803191282616; // cos(55 degrees)
    const float ARROW_HEAD_BASE_OFFSET = -0.35;
    const float ARROW_BODY_WIDTH = 0.25;
    const float DOTTING_WIDTH = 0.1;
    const float DOTTING_DOUBLE_WIDTH = DOTTING_WIDTH * 2.0;
    const float2 DOTTING_OFFSET = float2(0.0, DOTTING_WIDTH * -0.5);

    float oneMinusFill = 1.0 - fill;

    float2 lineScale = (0.5).xx / scale;
    float2 scaledFullLineWidth = FULL_LINE_WIDTH.xx * lineScale;

    float2 animatedCoords = float2(texcoord.x, texcoord.y + oneMinusFill);
    float4 centeredCoords = float4(texcoord, animatedCoords) * 2.0 - (1.0).xxxx;
    centeredCoords.x = abs(centeredCoords.x);
    centeredCoords.z = abs(centeredCoords.z);

    float fillfade = ((animatedCoords.y * animatedCoords.y * 0.5) * FILL_FADE_SIZE - FILL_FADE_OFFSET) * 0.75;

    // Cut arrow out of the fill.
    if(useArrow > 0.5) {
        float2 worldSpaceArrowScale = scale / min(scale.x, scale.y);
        float2 arrowCoords = (centeredCoords.zw - ARROW_ORIGIN_OFFSET) * worldSpaceArrowScale;

        float arrowHeadPoint = step(dot(normalize(arrowCoords), float2(0.0, -1.0)), COS_ARROW_HEAD_POINT_ANGLE);
        float arrowHeadBase = step(arrowCoords.y, ARROW_HEAD_BASE_OFFSET);

        fillfade *= saturate(arrowHeadPoint + arrowHeadBase);
        fillfade *= saturate(2.0 - (step(arrowCoords.x, ARROW_BODY_WIDTH) + arrowHeadBase));
    }

    float4 outlines = step((1.0).xxxx - scaledFullLineWidth.xyxy * 2.0, centeredCoords);

    float2 fillEdgeLine = outlines.zw;
    float fillAreaExclude = step(animatedCoords.y, 1.0);

    // Initial fill shape fading to the tail end with the 100% fill line around it.
    float fillBox = saturate(fillfade + fillEdgeLine.x + fillEdgeLine.y) * fillAreaExclude;

    // Fade out the animation towards the tail end nicely.
    float endFade = 1.0 - pow(1.0 - texcoord.y, 2.0);
    fillBox *= endFade;

    // Dotted outline frame
    float2 dotting = step((DOTTING_WIDTH).xx, fmod(texcoord * scale + DOTTING_OFFSET, (DOTTING_DOUBLE_WIDTH).xx));
    float2 outerLineExclude = outlines.yx;
    dotting *= outerLineExclude;

    // Fade fill on the inside of the frame box
    float2 frameBoxFillFade = float2(abs(centeredCoords.x), abs((saturate(animatedCoords.y - 1.0) / oneMinusFill) * 2.0 - 1.0));
    frameBoxFillFade = smoothstep((FRONT_FILL_FADE_START).xx, (1.0 + FRONT_FILL_FADE_END_OFFSET).xx, frameBoxFillFade) * FRONT_FILL_FADE_START;

    // Exclude the fade fill from the dotted line area
    float2 outlineExclude = step(centeredCoords.xy, (1.0).xx - scaledFullLineWidth * 2.0);
    frameBoxFillFade *= outlineExclude.x * outlineExclude.y;

    float frameBox = saturate(dotting.x + dotting.y + frameBoxFillFade.x + frameBoxFillFade.y);

    // Cut the frame box from overlapping with the fill box, also fade out nicely if the fill is 0.0
    float frameBoxExclude = step(1.0, animatedCoords.y);
    frameBox *= frameBoxExclude * endFade;

    return saturate(frameBox + fillBox);
}

void GetIndicatorAlpha_float(float2 texcoords, float3 worldScale, float shape, float fill, float useArrow, float coneRadius, out float alpha)
{
    float2 scale = float2(worldScale.x, worldScale.z);

    if(shape < 0.5)
    {
        alpha = rectAlpha(texcoords, scale, fill, useArrow);
    }
    else
    {
        alpha = circleAlpha(texcoords, scale, fill, coneRadius);
    }
}