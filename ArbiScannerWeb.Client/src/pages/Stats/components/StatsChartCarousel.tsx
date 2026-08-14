import React, { useEffect, useRef, useState } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';

export interface StatsChartPanel {
    key: string;
    title: string;
    content: React.ReactNode;
}

interface StatsChartCarouselProps {
    panels: StatsChartPanel[];
}

// No carousel/swipe library exists in this codebase — CSS scroll-snap gives
// native touch-swipe for free; the dot indicator + arrow buttons here mirror
// GuideModal's step-dot pattern for keyboard/mouse users.
const StatsChartCarousel: React.FC<StatsChartCarouselProps> = ({ panels }) => {
    const { t } = useTranslation('stats');
    const containerRef = useRef<HTMLDivElement>(null);
    const [activeIndex, setActiveIndex] = useState(0);

    // Updates activeIndex directly (not just via the scroll listener below) so
    // button/dot navigation is reflected immediately, without depending on the
    // browser actually firing a scroll event from scrollIntoView.
    const scrollToIndex = (index: number) => {
        const clamped = Math.max(0, Math.min(panels.length - 1, index));
        const container = containerRef.current;
        const child = container?.children[clamped];
        if (child instanceof HTMLElement) {
            child.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });
        }
        setActiveIndex(clamped);
    };

    useEffect(() => {
        const container = containerRef.current;
        if (!container) return;

        let frame = 0;
        const onScroll = () => {
            cancelAnimationFrame(frame);
            frame = requestAnimationFrame(() => {
                const { scrollLeft, clientWidth } = container;
                if (clientWidth === 0) return;
                const index = Math.round(scrollLeft / clientWidth);
                setActiveIndex((prev) => (prev === index ? prev : index));
            });
        };

        container.addEventListener('scroll', onScroll, { passive: true });
        return () => {
            container.removeEventListener('scroll', onScroll);
            cancelAnimationFrame(frame);
        };
    }, []);

    if (panels.length === 0) return null;

    const activeTitle = panels[Math.min(activeIndex, panels.length - 1)]?.title ?? '';

    return (
        <div className="w-full">
            <div className="flex items-center justify-between gap-3 mb-3">
                <button
                    type="button"
                    onClick={() => scrollToIndex(activeIndex - 1)}
                    disabled={activeIndex === 0}
                    aria-label={t('carousel.previous')}
                    className="p-2 rounded-full bg-white dark:bg-gray-800 shadow-sm hover:shadow-md transition-shadow text-gray-700 dark:text-gray-300 disabled:opacity-30 disabled:cursor-not-allowed disabled:shadow-none shrink-0"
                >
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
                    </svg>
                </button>

                <div className="relative flex-1 min-w-0 h-7">
                    <AnimatePresence mode="wait">
                        <motion.h3
                            key={activeIndex}
                            initial={{ opacity: 0 }}
                            animate={{ opacity: 1 }}
                            exit={{ opacity: 0 }}
                            transition={{ duration: 0.15 }}
                            className="absolute inset-0 text-lg font-semibold text-center dark:text-gray-100 truncate"
                        >
                            {activeTitle}
                        </motion.h3>
                    </AnimatePresence>
                </div>

                <button
                    type="button"
                    onClick={() => scrollToIndex(activeIndex + 1)}
                    disabled={activeIndex === panels.length - 1}
                    aria-label={t('carousel.next')}
                    className="p-2 rounded-full bg-white dark:bg-gray-800 shadow-sm hover:shadow-md transition-shadow text-gray-700 dark:text-gray-300 disabled:opacity-30 disabled:cursor-not-allowed disabled:shadow-none shrink-0"
                >
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                    </svg>
                </button>
            </div>

            <div
                ref={containerRef}
                className="flex overflow-x-auto snap-x snap-mandatory scroll-smooth gap-4 pb-1 [scrollbar-width:none] [-ms-overflow-style:none] [&::-webkit-scrollbar]:hidden"
            >
                {panels.map((panel) => (
                    <div key={panel.key} className="snap-center shrink-0 w-full">
                        {panel.content}
                    </div>
                ))}
            </div>

            {panels.length > 1 && (
                <div className="flex justify-center gap-2 mt-3">
                    {panels.map((panel, i) => (
                        <button
                            type="button"
                            key={panel.key}
                            onClick={() => scrollToIndex(i)}
                            aria-label={t('carousel.goTo', { title: panel.title })}
                            className={`h-2 rounded-full transition-all duration-200 ${
                                i === activeIndex
                                    ? 'bg-indigo-600 w-6'
                                    : 'bg-gray-200 dark:bg-gray-600 hover:bg-gray-300 dark:hover:bg-gray-500 w-2'
                            }`}
                        />
                    ))}
                </div>
            )}
        </div>
    );
};

export default StatsChartCarousel;
