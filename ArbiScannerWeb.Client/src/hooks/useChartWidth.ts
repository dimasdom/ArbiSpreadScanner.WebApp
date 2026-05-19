import { useEffect, useState } from 'react';

export function useChartWidth(mobileWidth = 0.9, desktopWidth = 800, breakpoint = 768): number {
    const [chartWidth, setChartWidth] = useState<number>(() => {
        if (typeof window !== 'undefined') {
            return window.innerWidth <= breakpoint
                ? Math.floor(window.innerWidth * mobileWidth)
                : desktopWidth;
        }
        return desktopWidth;
    });

    useEffect(() => {
        const handleResize = () => {
            setChartWidth(
                window.innerWidth <= breakpoint
                    ? Math.floor(window.innerWidth * mobileWidth)
                    : desktopWidth,
            );
        };
        window.addEventListener('resize', handleResize);
        handleResize();
        return () => window.removeEventListener('resize', handleResize);
    }, [mobileWidth, desktopWidth, breakpoint]);

    return chartWidth;
}
