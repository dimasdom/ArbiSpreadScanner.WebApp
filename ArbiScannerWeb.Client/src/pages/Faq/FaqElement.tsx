import React, { useState } from 'react';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';

interface FaqProps {
    question: string;
    answer: React.ReactNode;
}

const FaqElement: React.FC<FaqProps> = ({ question, answer }) => {
    const [isOpen, setIsOpen] = useState(false);

    return (
        <div className="bg-white dark:bg-gray-800 last:border-0 shadow-lg hover:shadow-2xl transition-shadow rounded-2xl">
            <button
                className="w-full py-5 flex bg-white dark:bg-gray-800 border-none justify-between items-center text-left focus:outline-none group px-4 rounded-2xl"
                onClick={() => setIsOpen(!isOpen)}
            >
                <span className={`text-base sm:text-lg font-semibold pr-4 ${isOpen ? 'text-indigo-600' : 'text-gray-900 dark:text-gray-100 group-hover:text-indigo-600'}`}>
                    {question}
                </span>
                <span 
                    className={`transform transition-transform duration-300 ease-in-out text-gray-400 group-hover:text-indigo-500 ${isOpen ? 'rotate-180 text-indigo-500' : ''}`}
                >
                    <ExpandMoreIcon />
                </span>
            </button>
            <div
                className={`overflow-hidden transition-all duration-300 ease-in-out px-4 ${
                    isOpen ? 'max-h-[2000px] opacity-100 mb-6' : 'max-h-0 opacity-0'
                }`}
            >
                <div className="text-gray-600 dark:text-gray-400 leading-relaxed text-sm sm:text-base py-2 wrap-break-word">
                    {answer}
                </div>
            </div>
        </div>
    );
};

export default FaqElement;
