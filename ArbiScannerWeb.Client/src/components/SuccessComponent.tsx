import * as React from 'react';

interface SuccessProps {
    message: string;
    title?: string;
    className?: string;
}

export default function SuccessComponent({ message, title = 'Success', className = '' }: SuccessProps) {
    return (
        <div className={`w-full max-w-md ${className}`}>
            <div className="bg-white rounded-2xl shadow-md ring-1 ring-gray-50 ring-inset p-6">
                <div className="flex items-start gap-4">
                    <div className="flex items-center justify-center h-12 w-12 rounded-full bg-green-50 ring-1 ring-green-100">
                        <svg className="h-6 w-6 text-green-600" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden>
                            <path d="M5 13l4 4L19 7" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                    </div>

                    <div className="flex-1">
                        <h3 className="text-lg font-semibold text-gray-800">{title}</h3>
                        <p className="mt-2 text-sm text-green-700 bg-green-50 rounded-md px-3 py-2 shadow-md ring-1 ring-green-100 ring-inset">{message}</p>
                    </div>
                </div>
            </div>
        </div>
    );
}
