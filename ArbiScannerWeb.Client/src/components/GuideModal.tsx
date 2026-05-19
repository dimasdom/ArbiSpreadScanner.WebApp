import React, { useEffect, useState } from 'react';

export interface GuideStep {
    icon: React.ReactNode;
    title: string;
    description: React.ReactNode;
}

interface GuideModalProps {
    storageKey: string;
    title: string;
    steps: GuideStep[];
}

const GuideModal: React.FC<GuideModalProps> = ({ storageKey, title, steps }) => {
    const [isVisible, setIsVisible] = useState(false);
    const [currentStep, setCurrentStep] = useState(0);

    useEffect(() => {
        const allDisabled = localStorage.getItem('guides-all-disabled');
        const seen = localStorage.getItem(storageKey);
        if (!allDisabled && !seen) {
            setIsVisible(true);
        }
    }, [storageKey]);

    const handleClose = () => {
        setIsVisible(false);
    };

    const handleDontShowAgain = () => {
        localStorage.setItem('guides-all-disabled', 'true');
        setIsVisible(false);
    };

    const handleGotIt = () => {
        localStorage.setItem(storageKey, 'true');
        setIsVisible(false);
    };

    const handleNext = () => {
        if (currentStep < steps.length - 1) {
            setCurrentStep((s) => s + 1);
        }
    };

    const handleBack = () => {
        if (currentStep > 0) {
            setCurrentStep((s) => s - 1);
        }
    };

    if (!isVisible) return null;

    const step = steps[currentStep];
    const isFirst = currentStep === 0;
    const isLast = currentStep === steps.length - 1;

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center backdrop-blur-md bg-white/50">
            {/* Backdrop close button */}
            <button
                className="absolute opacity-0 inset-0 w-full h-full cursor-default"
                onClick={handleClose}
                aria-label="Close guide"
                tabIndex={-1}
            />
            <dialog
                open
                className="relative bg-white w-full sm:max-w-lg sm:mx-4 sm:rounded-2xl shadow-2xl flex flex-col max-h-[90dvh] sm:max-h-[80vh] rounded-t-2xl overflow-hidden m-0 p-0 border-0"
                aria-label={title}
            >
                {/* Header */}
                <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 bg-linear-to-r from-indigo-600 to-blue-600 text-white rounded-t-2xl">
                    <div className="flex items-center gap-2">
                        <svg className="w-5 h-5 opacity-80" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                        </svg>
                        <h2 className="text-base font-bold tracking-wide">{title}</h2>
                    </div>
                    <button
                        onClick={handleClose}
                        className="text-white/70 hover:text-white transition-colors p-1 rounded-lg hover:bg-white/10"
                        aria-label="Close guide"
                    >
                        <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>

                {/* Step content */}
                <div className="flex-1 overflow-y-auto px-6 py-8 flex flex-col items-center text-center gap-4">
                    <div className="w-16 h-16 rounded-2xl bg-indigo-50 flex items-center justify-center text-indigo-600 shrink-0">
                        {step.icon}
                    </div>
                    <h3 className="text-xl font-bold text-gray-900">{step.title}</h3>
                    <div className="text-gray-600 text-sm leading-relaxed max-w-sm">{step.description}</div>
                </div>

                {/* Progress dots */}
                {steps.length > 1 && (
                    <div className="flex justify-center gap-2 pb-2">
                        {steps.map((s, i) => (
                            <button
                                key={s.title}
                                onClick={() => setCurrentStep(i)}
                                aria-label={`Go to step ${i + 1}`}
                                className={`w-2 h-2 rounded-full transition-all duration-200 ${
                                    i === currentStep
                                        ? 'bg-indigo-600 w-6'
                                        : 'bg-gray-200 hover:bg-gray-300'
                                }`}
                            />
                        ))}
                    </div>
                )}

                {/* Footer */}
                <div className="px-6 py-4 border-t border-gray-100 flex items-center justify-between gap-3">
                    <button
                        onClick={handleDontShowAgain}
                        className="px-5 py-2 rounded-xl text-sm font-semibold bg-indigo-600 text-white hover:bg-indigo-700 transition-colors shadow-sm shrink-0"
                    >
                        Don't show again
                    </button>

                    <div className="flex gap-2">
                        {!isFirst && (
                            <button
                                onClick={handleBack}
                                className="px-4 py-2 rounded-xl text-sm font-medium bg-gray-100 text-gray-700 hover:bg-gray-200 transition-colors"
                            >
                                Back
                            </button>
                        )}
                        {isLast ? (
                            <button
                                onClick={handleGotIt}
                                className="px-5 py-2 rounded-xl text-sm font-semibold bg-indigo-600 text-white hover:bg-indigo-700 transition-colors shadow-sm"
                            >
                                Got it!
                            </button>
                        ) : (
                            <button
                                onClick={handleNext}
                                className="px-5 py-2 rounded-xl text-sm font-semibold bg-indigo-600 text-white hover:bg-indigo-700 transition-colors shadow-sm flex items-center gap-1"
                            >
                                Next
                                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                                </svg>
                            </button>
                        )}
                    </div>
                </div>
            </dialog>
        </div>
    );
};

export default GuideModal;
